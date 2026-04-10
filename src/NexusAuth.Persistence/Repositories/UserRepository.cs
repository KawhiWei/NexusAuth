using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.AggregateRoots.Users;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public class UserRepository(IUnitOfWork unitOfWork) : EfCoreAggregateRootRepository<User, Guid>(unitOfWork), IUserRepository
{
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
        await unitOfWork.CommitAsync(ct);
    }
}
