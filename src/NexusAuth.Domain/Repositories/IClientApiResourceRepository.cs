using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.AggregateRoots.ApiResources;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

/// <summary>
/// ClientApiResource uses composite PK (no surrogate Id), so it does not extend IEntityRepository.
/// </summary>
public interface IClientApiResourceRepository : IScopedDependency
{
    Task<IReadOnlyList<ApiResource>> GetResourcesByClientIdAsync(Guid clientId, CancellationToken ct = default);

    Task AddAsync(ClientApiResource association, CancellationToken ct = default);

    Task RemoveAsync(Guid clientId, Guid apiResourceId, CancellationToken ct = default);
}
