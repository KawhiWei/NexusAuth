using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

public interface IScimServicePrincipalCredentialRepository
    : IEntityRepository<ScimServicePrincipalCredential, Guid>, IScopedDependency
{
    Task<ScimServicePrincipalCredential?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken ct = default);

    Task<ScimServicePrincipalCredential?> FindByNameAsync(
        string name,
        CancellationToken ct = default);

    Task<ScimServicePrincipalCredential?> FindByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ScimServicePrincipalCredential>> GetAllAsync(CancellationToken ct = default);

    Task AddAsync(
        ScimServicePrincipalCredential credential,
        CancellationToken ct = default);

    Task UpdateAsync(
        ScimServicePrincipalCredential credential,
        CancellationToken ct = default);
}
