using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

public class AuthorizationCode : EntityWithIdentity<Guid>
{
    /// <summary>
    /// SHA-256 hash of the one-time code, encoded as unpadded Base64Url.
    /// The raw code is deliberately not part of the entity and must only be
    /// carried by <see cref="AuthorizationCodeCreationResult"/> until sent to
    /// the client.
    /// </summary>
    public string CodeHash { get; private set; } = default!;

    public string ClientId { get; private set; } = default!;

    public Guid UserId { get; private set; }

    public string RedirectUri { get; private set; } = default!;

    public string Scope { get; private set; } = default!;

    public string? CodeChallenge { get; private set; }

    public string? CodeChallengeMethod { get; private set; }

    public string? Nonce { get; private set; }

    public string? ClaimsJson { get; private set; }

    public DateTimeOffset? AuthenticatedAt { get; private set; }

    public string? Acr { get; private set; }

    public string? Amr { get; private set; }

    public bool IsUsed { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// EF Core constructor
    /// </summary>
    private AuthorizationCode(Guid id) : base(id)
    {
    }

    public static AuthorizationCodeCreationResult Create(
        string clientId,
        Guid userId,
        string redirectUri,
        string scope,
        string? codeChallenge = null,
        string? codeChallengeMethod = null,
        string? nonce = null,
        string? claimsJson = null,
        DateTimeOffset? authenticatedAt = null,
        string? acr = null,
        string? amr = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var now = DateTimeOffset.UtcNow;

        var rawCode = GenerateUrlSafeRandomString(32);
        var entity = new AuthorizationCode(Guid.NewGuid())
        {
            CodeHash = Hash(rawCode),
            ClientId = clientId,
            UserId = userId,
            RedirectUri = redirectUri,
            Scope = scope,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            Nonce = nonce,
            ClaimsJson = claimsJson,
            AuthenticatedAt = authenticatedAt,
            Acr = acr,
            Amr = amr,
            IsUsed = false,
            ExpiresAt = now.AddMinutes(10),
            CreatedAt = now,
        };

        return new AuthorizationCodeCreationResult(entity, rawCode);
    }

    public static string Hash(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(code)))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public void MarkAsUsed()
    {
        IsUsed = true;
    }

    private static string GenerateUrlSafeRandomString(int byteLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

public sealed record AuthorizationCodeCreationResult(AuthorizationCode Entity, string RawCode);
