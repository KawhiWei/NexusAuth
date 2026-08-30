namespace NexusAuth.Application.Services.LoginAudits;

public sealed class LoginAuditService(ILoginAuditLogRepository auditLogRepository) : ILoginAuditService
{
    public Task RecordAsync(LoginAuditRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var auditLog = LoginAuditLog.Create(
            record.Username,
            record.UserId,
            record.ClientId,
            record.IsSuccessful,
            record.FailureReason,
            record.IpAddress,
            record.UserAgent);
        return auditLogRepository.AddAsync(auditLog, ct);
    }

    public async Task<PagedResult<LoginAuditLogDto>> GetPagedAsync(
        string? keyword,
        bool? isSuccessful,
        string? clientId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
        var (items, total) = await auditLogRepository.GetPagedAsync(
            keyword, isSuccessful, clientId, normalizedPage, normalizedPageSize, ct);

        return new PagedResult<LoginAuditLogDto>(items.Select(Map).ToList(), total, normalizedPage, normalizedPageSize);
    }

    public async Task<IReadOnlyList<LoginAuditLogDto>> GetRecentForUserAsync(
        Guid userId,
        int count,
        CancellationToken ct = default)
    {
        var items = await auditLogRepository.GetRecentForUserAsync(userId, count, ct);
        return items.Select(Map).ToList();
    }

    private static LoginAuditLogDto Map(LoginAuditLog auditLog) => new(
        auditLog.Id,
        auditLog.Username,
        auditLog.UserId,
        auditLog.ClientId,
        auditLog.IsSuccessful,
        auditLog.FailureReason,
        auditLog.IpAddress,
        auditLog.UserAgent,
        auditLog.OccurredAt);
}
