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

    public async Task<User?> FindByExternalIdAsync(string externalId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        return await FindAll(u => u.ExternalId == externalId.Trim()).FirstOrDefaultAsync(ct);
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

    public async Task<(IReadOnlyList<User> Items, int Total)> GetScimPagedAsync(
        string? userName,
        string? externalId,
        bool? isActive,
        string? email,
        int startIndex,
        int count,
        CancellationToken ct = default)
    {
        var query = FindAll();
        if (!string.IsNullOrWhiteSpace(userName))
            query = query.Where(user => user.Username == userName);
        if (!string.IsNullOrWhiteSpace(externalId))
            query = query.Where(user => user.ExternalId == externalId);
        if (isActive.HasValue)
            query = query.Where(user => user.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(user => user.Email == email.ToLowerInvariant());

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(user => user.Id)
            .Skip(startIndex - 1)
            .Take(count)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<(IReadOnlyList<User> Items, int Total)> GetAdminPagedAsync(
        string? keyword,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = FindAll();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.Trim();
            query = query.Where(user => user.Username.Contains(value)
                || user.Nickname.Contains(value)
                || (user.Email != null && user.Email.Contains(value))
                || (user.ExternalId != null && user.ExternalId.Contains(value)));
        }
        if (isActive.HasValue)
            query = query.Where(user => user.IsActive == isActive.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(user => user.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
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

    public async Task DeleteAsync(User user, CancellationToken ct = default)
    {
        Remove(user);
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

    public async Task<bool> TryUseTotpCounterAsync(
        Guid userId,
        long counter,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(counter);

        var affected = await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE nexusauth.users
            SET totp_last_used_counter = {counter},
                updated_at = {now}
            WHERE id = {userId}
              AND is_active = true
              AND totp_enabled = true
              AND totp_secret_protected IS NOT NULL
              AND (totp_last_used_counter IS NULL OR totp_last_used_counter < {counter});
            """, ct);

        return affected == 1;
    }

    public async Task<bool> TryConfirmTotpEnrollmentAsync(
        Guid userId,
        string expectedProtectedSecret,
        long counter,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedProtectedSecret);
        ArgumentOutOfRangeException.ThrowIfNegative(counter);

        // The pending ciphertext is compared in SQL and promoted in the same
        // statement. This makes the confirmation token one-shot even when two
        // requests arrive concurrently on different application instances.
        var affected = await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE nexusauth.users
            SET totp_secret_protected = totp_pending_secret_protected,
                totp_pending_secret_protected = NULL,
                totp_pending_expires_at = NULL,
                totp_enabled = true,
                totp_last_used_counter = {counter},
                updated_at = {now}
            WHERE id = {userId}
              AND is_active = true
              AND totp_pending_secret_protected = {expectedProtectedSecret}
              AND totp_pending_expires_at IS NOT NULL
              AND totp_pending_expires_at > {now};
            """, ct);

        return affected == 1;
    }
}
