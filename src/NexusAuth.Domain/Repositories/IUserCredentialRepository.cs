using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

public interface IUserCredentialRepository : IEntityRepository<UserCredential, Guid>, IScopedDependency
{
    Task AddAsync(UserCredential credential, CancellationToken ct = default);
    Task<IReadOnlyList<UserCredential>> GetEnabledTotpAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserCredential>> GetTotpAsync(Guid userId, CancellationToken ct = default);
    Task<UserCredential?> FindPendingTotpAsync(Guid userId, string protectedSecret, CancellationToken ct = default);
    Task<bool> TryConfirmTotpAsync(Guid credentialId, Guid userId, string expectedProtectedSecret, long counter, DateTimeOffset now, CancellationToken ct = default);
    Task<bool> TryUseTotpCounterAsync(Guid credentialId, Guid userId, long counter, DateTimeOffset now, CancellationToken ct = default);
    Task<bool> DisableAsync(Guid credentialId, Guid userId, DateTimeOffset now, CancellationToken ct = default);
}
