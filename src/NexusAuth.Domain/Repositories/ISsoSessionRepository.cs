using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

public interface ISsoSessionRepository : IEntityRepository<SsoSession, Guid>, IScopedDependency
{
    Task AddAsync(SsoSession session, CancellationToken ct = default);

    Task<SsoSession?> FindActiveAsync(Guid sessionId, Guid userId, DateTimeOffset now, CancellationToken ct = default);

    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default);
}
