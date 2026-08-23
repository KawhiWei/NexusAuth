using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.AggregateRoots.Users;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public class UserRepository : EfCoreAggregateRootRepository<User, Guid>, IUserRepository
{
    private readonly LuckDbContextBase _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public UserRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
            ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");
    }

    public async Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default)
    {
        return await FindAll(u => u.Username == username).FirstOrDefaultAsync(ct);
    }

    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.ToLowerInvariant();
        return await FindAll(u => u.Email == normalizedEmail).FirstOrDefaultAsync(ct);
    }

    public async Task<User?> FindByPhoneNumberAsync(string phoneNumber, CancellationToken ct = default)
    {
        return await FindAll(u => u.PhoneNumber == phoneNumber).FirstOrDefaultAsync(ct);
    }

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await FindAsync(id);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        Add(user);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        Update(user);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task<User?> RegisterFailedLoginAsync(
        Guid userId,
        int failureLimit,
        TimeSpan lockoutDuration,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(failureLimit);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lockoutDuration, TimeSpan.Zero);

        var lockoutUntil = now.Add(lockoutDuration);
        // Keep the counter and lockout decision in one SQL UPDATE. This
        // prevents concurrent password attempts from overwriting each other
        // after both requests loaded the same User entity.
        await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE nexusauth.users
            SET failed_login_attempts = CASE
                    WHEN locked_until IS NOT NULL AND locked_until <= {now}
                        THEN 1
                    WHEN failed_login_attempts + 1 >= {failureLimit}
                        THEN 0
                    ELSE failed_login_attempts + 1
                END,
                locked_until = CASE
                    WHEN locked_until IS NOT NULL AND locked_until <= {now}
                        AND 1 >= {failureLimit}
                        THEN {lockoutUntil}
                    WHEN (locked_until IS NULL OR locked_until <= {now})
                        AND failed_login_attempts + 1 >= {failureLimit}
                        THEN {lockoutUntil}
                    WHEN locked_until IS NOT NULL AND locked_until <= {now}
                        THEN NULL
                    ELSE locked_until
                END,
                updated_at = {now}
            WHERE id = {userId}
              AND is_active = true
              AND (locked_until IS NULL OR locked_until <= {now});
            """, ct);

        return await FindByIdAsync(userId, ct);
    }

    public async Task ResetLoginFailuresAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        await _dbContext.Set<User>()
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(u => u.FailedLoginAttempts, 0)
                    .SetProperty(u => u.LockedUntil, (DateTimeOffset?)null)
                    .SetProperty(u => u.UpdatedAt, now),
                ct);
    }
}
