using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.AggregateRoots.Users;

namespace NexusAuth.Domain.Repositories;

public interface IUserRepository : IAggregateRootRepository<User, Guid>, IScopedDependency
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default);

    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);

    Task<User?> FindByPhoneNumberAsync(string phoneNumber, CancellationToken ct = default);

    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(User user, CancellationToken ct = default);
}
