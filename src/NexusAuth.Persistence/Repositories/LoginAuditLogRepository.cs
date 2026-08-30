using Luck.EntityFrameworkCore.DbContexts;
using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public sealed class LoginAuditLogRepository(IUnitOfWork unitOfWork)
    : EfCoreEntityRepository<LoginAuditLog, Guid>(unitOfWork), ILoginAuditLogRepository
{
    private readonly LuckDbContextBase _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
        ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task AddAsync(LoginAuditLog auditLog, CancellationToken ct = default)
    {
        _dbContext.Add(auditLog);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task<(IReadOnlyList<LoginAuditLog> Items, int Total)> GetPagedAsync(
        string? keyword,
        bool? isSuccessful,
        string? clientId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = FindAll().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.Trim();
            query = query.Where(log => log.Username.Contains(value)
                || (log.ClientId != null && log.ClientId.Contains(value))
                || (log.IpAddress != null && log.IpAddress.Contains(value)));
        }
        if (isSuccessful.HasValue)
            query = query.Where(log => log.IsSuccessful == isSuccessful.Value);
        if (!string.IsNullOrWhiteSpace(clientId))
            query = query.Where(log => log.ClientId == clientId.Trim());

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(log => log.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<LoginAuditLog>> GetRecentForUserAsync(
        Guid userId,
        int count,
        CancellationToken ct = default)
    {
        return await FindAll().AsNoTracking()
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.OccurredAt)
            .Take(Math.Clamp(count, 1, 20))
            .ToListAsync(ct);
    }
}
