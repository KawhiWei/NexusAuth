using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.AggregateRoots.ApiResources;

namespace NexusAuth.Domain.Repositories;

public interface IApiResourceRepository : IAggregateRootRepository<ApiResource, Guid>, IScopedDependency
{
    Task<ApiResource?> FindByNameAsync(string name, CancellationToken ct = default);

    Task<IReadOnlyList<ApiResource>> FindByNamesAsync(IEnumerable<string> names, CancellationToken ct = default);

    Task<ApiResource?> FindByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ApiResource>> GetAllActiveAsync(CancellationToken ct = default);

    Task AddAsync(ApiResource resource, CancellationToken ct = default);
}
