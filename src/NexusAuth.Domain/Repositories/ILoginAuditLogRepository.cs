using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

public interface ILoginAuditLogRepository : IEntityRepository<LoginAuditLog, Guid>, IScopedDependency
{
    Task AddAsync(LoginAuditLog auditLog, CancellationToken ct = default);

    Task<(IReadOnlyList<LoginAuditLog> Items, int Total)> GetPagedAsync(
        string? keyword,
        bool? isSuccessful,
        string? clientId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<LoginAuditLog>> GetRecentForUserAsync(
        Guid userId,
        int count,
        CancellationToken ct = default);
}
