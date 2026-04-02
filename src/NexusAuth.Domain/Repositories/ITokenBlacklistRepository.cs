using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

public interface ITokenBlacklistRepository : IEntityRepository<TokenBlacklistEntry, Guid>, IScopedDependency
{
    Task<TokenBlacklistEntry?> FindByJtiAsync(string jti, CancellationToken ct = default);

    Task<bool> ExistsActiveAsync(string jti, DateTimeOffset now, CancellationToken ct = default);

    Task AddAsync(TokenBlacklistEntry entry, CancellationToken ct = default);
}
