using Luck.DDD.Domain.Domain.AggregateRoots;

namespace NexusAuth.Domain.AggregateRoots.OAuthClients;

public class OAuthClient : AggregateRootWithIdentity<Guid>
{
    public string ClientId { get; private set; } = default!;

    public string ClientSecretHash { get; private set; } = default!;

    public string ClientName { get; private set; } = default!;

    public string? Description { get; private set; }

    public List<string> RedirectUris { get; private set; } = new();

    public List<string> PostLogoutRedirectUris { get; private set; } = new();

    public List<string> AllowedScopes { get; private set; } = new();

    public List<string> AllowedGrantTypes { get; private set; } = new();

    public bool RequirePkce { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// EF Core constructor
    /// </summary>
    private OAuthClient(Guid id) : base(id)
    {
    }

    public static OAuthClient Create(
        string clientId,
        string rawClientSecret,
        string clientName,
        string? description = null,
        IEnumerable<string>? redirectUris = null,
        IEnumerable<string>? postLogoutRedirectUris = null,
        IEnumerable<string>? allowedScopes = null,
        IEnumerable<string>? allowedGrantTypes = null,
        bool requirePkce = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawClientSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        var client = new OAuthClient(Guid.NewGuid())
        {
            ClientId = clientId,
            ClientSecretHash = BCrypt.Net.BCrypt.HashPassword(rawClientSecret, workFactor: 12),
            ClientName = clientName,
            Description = description,
            RedirectUris = redirectUris?.ToList() ?? new List<string>(),
            PostLogoutRedirectUris = postLogoutRedirectUris?.ToList() ?? new List<string>(),
            AllowedScopes = allowedScopes?.ToList() ?? new List<string>(),
            AllowedGrantTypes = allowedGrantTypes?.ToList() ?? new List<string>(),
            RequirePkce = requirePkce,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return client;
    }

    public bool VerifyClientSecret(string rawSecret)
    {
        return BCrypt.Net.BCrypt.Verify(rawSecret, ClientSecretHash);
    }

    public bool IsValidRedirectUri(string uri)
    {
        return RedirectUris.Contains(uri, StringComparer.Ordinal);
    }

    /// <summary>
    /// 用于 OIDC RP-Initiated Logout 场景，校验 RP 传入的 post_logout_redirect_uri 是否已登记。
    /// 主要调用方：Host 层的 /connect/endsession 端点。
    /// </summary>
    public bool IsValidPostLogoutRedirectUri(string uri)
    {
        return PostLogoutRedirectUris.Contains(uri, StringComparer.Ordinal);
    }

    public bool IsGrantTypeAllowed(string grantType)
    {
        return AllowedGrantTypes.Contains(grantType, StringComparer.OrdinalIgnoreCase);
    }
}
