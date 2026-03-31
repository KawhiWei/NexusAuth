using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

public interface IAuthorizationCodeRepository : IEntityRepository<AuthorizationCode, Guid>, IScopedDependency
{
    Task<AuthorizationCode?> FindByCodeAsync(string code, CancellationToken ct = default);

    Task AddAsync(AuthorizationCode code, CancellationToken ct = default);

    Task MarkUsedAsync(Guid id, CancellationToken ct = default);
}
