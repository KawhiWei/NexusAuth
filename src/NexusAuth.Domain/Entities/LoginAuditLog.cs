using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

/// <summary>
/// Immutable audit record for an interactive username/password login attempt.
/// Secrets, credentials, and tokens are deliberately never part of this entity.
/// </summary>
public sealed class LoginAuditLog : EntityWithIdentity<Guid>
{
    public Guid? UserId { get; private set; }
    public string Username { get; private set; } = default!;
    public string? ClientId { get; private set; }
    public bool IsSuccessful { get; private set; }
    public string? FailureReason { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private LoginAuditLog(Guid id) : base(id)
    {
    }

    public static LoginAuditLog Create(
        string? username,
        Guid? userId,
        string? clientId,
        bool isSuccessful,
        string? failureReason,
        string? ipAddress,
        string? userAgent)
    {
        if (isSuccessful && userId is null)
            throw new ArgumentException("A successful login audit record must have a user ID.", nameof(userId));

        return new LoginAuditLog(Guid.NewGuid())
        {
            Username = Limit(username, 100, "(empty)")!,
            UserId = userId,
            ClientId = Limit(clientId, 128),
            IsSuccessful = isSuccessful,
            FailureReason = isSuccessful ? null : Limit(failureReason, 64, "Unknown"),
            IpAddress = Limit(ipAddress, 45),
            UserAgent = Limit(userAgent, 1024),
            OccurredAt = DateTimeOffset.UtcNow,
        };
    }

    private static string? Limit(string? value, int maxLength, string? fallback = null)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
            return fallback;

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
