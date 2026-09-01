using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.AggregateRoots.OAuthClients;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

public interface IOAuthClientRepository : IAggregateRootRepository<OAuthClient, Guid>, IScopedDependency
{
    Task<OAuthClient?> FindByClientIdAsync(string clientId, CancellationToken ct = default);

    Task AddAsync(OAuthClient client, CancellationToken ct = default);

    Task UpdateAsync(OAuthClient client, CancellationToken ct = default);

    Task ReplaceSharedSecretAsync(Guid clientId, OAuthClientSecret secret, CancellationToken ct = default);

    Task DeleteAsync(OAuthClient client, CancellationToken ct = default);

    Task<(List<OAuthClient> Items, int Total)> GetPagedAsync(
        string? keyword,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<OAuthClient?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
