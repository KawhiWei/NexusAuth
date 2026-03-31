using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

public interface IRefreshTokenRepository : IEntityRepository<RefreshToken, Guid>, IScopedDependency
{
    Task<RefreshToken?> FindByTokenAsync(string token, CancellationToken ct = default);

    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    Task RevokeAsync(Guid id, CancellationToken ct = default);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}
