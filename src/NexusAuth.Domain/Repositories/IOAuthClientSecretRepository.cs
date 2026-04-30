namespace NexusAuth.Domain.Repositories;

using NexusAuth.Domain.Entities;

public interface IOAuthClientSecretRepository : IEntityRepository<OAuthClientSecret, Guid>, IScopedDependency
{
    Task AddAsync(OAuthClientSecret secret, CancellationToken ct = default);
}
