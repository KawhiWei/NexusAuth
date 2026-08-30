using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

/// <summary>
/// A bearer credential used by an external SCIM provisioning client.
/// Only the hash of the raw token is persisted.
/// </summary>
public class ScimServicePrincipalCredential : EntityWithIdentity<Guid>
{
    public const int TokenHashLength = 43;

    public string Name { get; private set; } = default!;

    public string TokenHash { get; private set; } = default!;

    public List<string> Scopes { get; private set; } = [];

    public bool IsActive { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    private ScimServicePrincipalCredential(Guid id) : base(id)
    {
    }

    /// <summary>
    /// Creates a credential and returns the raw bearer token exactly once.
    /// </summary>
    public static ScimServicePrincipalCredentialCreationResult Create(
        string name,
        IEnumerable<string>? scopes = null,
        DateTimeOffset? expiresAt = null)
    {
        var rawToken = GenerateRawToken();
        return new(CreateWithToken(name, rawToken, scopes, expiresAt), rawToken);
    }

    /// <summary>
    /// Creates a credential from a caller-supplied raw token. The raw token is
    /// hashed immediately and is never retained by the entity.
    /// </summary>
    public static ScimServicePrincipalCredential CreateWithToken(
        string name,
        string rawToken,
        IEnumerable<string>? scopes = null,
        DateTimeOffset? expiresAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        return new ScimServicePrincipalCredential(Guid.NewGuid())
        {
            Name = name.Trim(),
            TokenHash = Hash(rawToken),
            Scopes = NormalizeScopes(scopes),
            IsActive = true,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Hashes a raw bearer token as unpadded Base64Url SHA-256.
    /// </summary>
    public static string Hash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public bool CanAuthenticate(DateTimeOffset now)
    {
        return IsActive
            && RevokedAt is null
            && (!ExpiresAt.HasValue || ExpiresAt.Value > now);
    }

    public bool CanAuthenticate() => CanAuthenticate(DateTimeOffset.UtcNow);

    public void RecordUse(DateTimeOffset? now = null)
    {
        LastUsedAt = now ?? DateTimeOffset.UtcNow;
    }

    public void Revoke(DateTimeOffset? revokedAt = null)
    {
        IsActive = false;
        RevokedAt ??= revokedAt ?? DateTimeOffset.UtcNow;
    }

    public void Update(
        string name,
        IEnumerable<string>? scopes,
        DateTimeOffset? expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Scopes = NormalizeScopes(scopes);
        ExpiresAt = expiresAt;
    }

    public void SetActive(bool isActive)
    {
        if (isActive)
        {
            IsActive = true;
            RevokedAt = null;
            return;
        }

        Revoke();
    }

    private static List<string> NormalizeScopes(IEnumerable<string>? scopes)
    {
        return scopes is null
            ? []
            : scopes
                .Where(scope => !string.IsNullOrWhiteSpace(scope))
                .Select(scope => scope.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
    }

    private static string GenerateRawToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

public sealed record ScimServicePrincipalCredentialCreationResult(
    ScimServicePrincipalCredential Entity,
    string RawToken);
