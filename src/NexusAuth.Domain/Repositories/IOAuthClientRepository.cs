using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.AggregateRoots.OAuthClients;

namespace NexusAuth.Domain.Repositories;

public interface IOAuthClientRepository : IAggregateRootRepository<OAuthClient, Guid>, IScopedDependency
{
    Task<OAuthClient?> FindByClientIdAsync(string clientId, CancellationToken ct = default);

    Task AddAsync(OAuthClient client, CancellationToken ct = default);
}
