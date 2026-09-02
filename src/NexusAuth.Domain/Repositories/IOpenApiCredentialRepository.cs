using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

public interface IOpenApiCredentialRepository : IEntityRepository<OpenApiCredential, Guid>, IScopedDependency
{
    Task<OpenApiCredential?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task<OpenApiCredential?> FindByNameAsync(string name, CancellationToken ct = default);
    Task<OpenApiCredential?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OpenApiCredential>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(OpenApiCredential credential, CancellationToken ct = default);
    Task UpdateAsync(OpenApiCredential credential, CancellationToken ct = default);
}
