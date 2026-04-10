using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.AggregateRoots.OAuthClients;

namespace NexusAuth.Domain.Repositories;

public interface IOAuthClientRepository : IAggregateRootRepository<OAuthClient, Guid>, IScopedDependency
{
    Task<OAuthClient?> FindByClientIdAsync(string clientId, CancellationToken ct = default);

    Task AddAsync(OAuthClient client, CancellationToken ct = default);

    Task UpdateAsync(OAuthClient client, CancellationToken ct = default);

    Task DeleteAsync(OAuthClient client, CancellationToken ct = default);

    Task<List<OAuthClient>> GetAllAsync(CancellationToken ct = default);

    Task<OAuthClient?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
