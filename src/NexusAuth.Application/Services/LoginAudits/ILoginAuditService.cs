namespace NexusAuth.Application.Services.LoginAudits;

public interface ILoginAuditService : IScopedDependency
{
    Task RecordAsync(LoginAuditRecord record, CancellationToken ct = default);

    Task<PagedResult<LoginAuditLogDto>> GetPagedAsync(
        string? keyword,
        bool? isSuccessful,
        string? clientId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<LoginAuditLogDto>> GetRecentForUserAsync(
        Guid userId,
        int count,
        CancellationToken ct = default);
}

public sealed record LoginAuditRecord(
    string? Username,
    Guid? UserId,
    string? ClientId,
    bool IsSuccessful,
    string? FailureReason,
    string? IpAddress,
    string? UserAgent);

public sealed record LoginAuditLogDto(
    Guid Id,
    string Username,
    Guid? UserId,
    string? ClientId,
    bool IsSuccessful,
    string? FailureReason,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset OccurredAt);
