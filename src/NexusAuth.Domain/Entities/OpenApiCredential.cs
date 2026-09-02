using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

/// <summary>
/// Long-lived bearer credential for a machine consumer of the NexusAuth
/// read-only Open API. The raw token is returned once and only its hash is persisted.
/// </summary>
public sealed class OpenApiCredential : EntityWithIdentity<Guid>
{
    public const int TokenHashLength = 43;
    public const string TargetTypeApplication = "application";
    public const string TargetTypeServiceResource = "service_resource";
    public const string ScopeApplicationRead = "application:read";
    public const string ScopeServiceResourceRead = "service_resource:read";

    public string Name { get; private set; } = default!;
    public string TokenHash { get; private set; } = default!;
    public string TargetType { get; private set; } = default!;
    public List<string> Scopes { get; private set; } = [];
    public bool IsActive { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private OpenApiCredential(Guid id) : base(id) { }

    public static OpenApiCredentialCreationResult Create(string name, string targetType, DateTimeOffset? expiresAt = null)
    {
        var rawToken = GenerateRawToken();
        return new(CreateWithToken(name, rawToken, targetType, expiresAt), rawToken);
    }

    public static OpenApiCredential CreateWithToken(string name, string rawToken, string targetType, DateTimeOffset? expiresAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        var normalizedTargetType = NormalizeTargetType(targetType);

        return new OpenApiCredential(Guid.NewGuid())
        {
            Name = name.Trim(),
            TokenHash = Hash(rawToken),
            TargetType = normalizedTargetType,
            Scopes = [GetReadScope(normalizedTargetType)],
            IsActive = true,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static string Hash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public bool CanAuthenticate(string requiredTargetType, string requiredScope, DateTimeOffset? now = null)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;
        return IsActive && RevokedAt is null
            && (!ExpiresAt.HasValue || ExpiresAt.Value > timestamp)
            && string.Equals(TargetType, requiredTargetType, StringComparison.Ordinal)
            && Scopes.Contains(requiredScope, StringComparer.Ordinal);
    }

    public void RecordUse(DateTimeOffset? now = null) => LastUsedAt = now ?? DateTimeOffset.UtcNow;

    public void Update(string name, DateTimeOffset? expiresAt, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        ExpiresAt = expiresAt;
        SetActive(isActive);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        RevokedAt = isActive ? null : DateTimeOffset.UtcNow;
    }

    public void Revoke() => SetActive(false);

    public static string NormalizeTargetType(string targetType)
    {
        var normalized = targetType?.Trim();
        if (normalized is TargetTypeApplication or TargetTypeServiceResource)
            return normalized;
        throw new ArgumentException("targetType must be 'application' or 'service_resource'.", nameof(targetType));
    }

    public static string GetReadScope(string targetType) => NormalizeTargetType(targetType) switch
    {
        TargetTypeApplication => ScopeApplicationRead,
        TargetTypeServiceResource => ScopeServiceResourceRead,
        _ => throw new InvalidOperationException(),
    };

    private static string GenerateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}

public sealed record OpenApiCredentialCreationResult(OpenApiCredential Entity, string Token);
