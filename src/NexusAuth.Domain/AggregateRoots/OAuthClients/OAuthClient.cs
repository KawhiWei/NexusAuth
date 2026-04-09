using Luck.DDD.Domain.Domain.AggregateRoots;
using System.Linq;

namespace NexusAuth.Domain.AggregateRoots.OAuthClients;

public class OAuthClient : AggregateRootWithIdentity<Guid>
{
    public const string TokenEndpointAuthMethodClientSecretBasic = "client_secret_basic";

    public const string TokenEndpointAuthMethodClientSecretPost = "client_secret_post";

    public const string TokenEndpointAuthMethodPrivateKeyJwt = "private_key_jwt";

    public const string ClientAssertionTypeJwtBearer = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

    public string ClientId { get; private set; } = default!;

    public List<OAuthClientSecret> ClientSecrets { get; private set; } = new();

    public string TokenEndpointAuthMethod { get; private set; } = TokenEndpointAuthMethodClientSecretBasic;

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
        string clientName,
        string? description = null,
        IEnumerable<string>? redirectUris = null,
        IEnumerable<string>? postLogoutRedirectUris = null,
        IEnumerable<string>? allowedScopes = null,
        IEnumerable<string>? allowedGrantTypes = null,
        bool requirePkce = true,
        string tokenEndpointAuthMethod = TokenEndpointAuthMethodClientSecretBasic,
        IEnumerable<OAuthClientSecret>? clientSecrets = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        var normalizedSecrets = clientSecrets?.ToList() ?? new List<OAuthClientSecret>();
        ValidateTokenEndpointAuthentication(tokenEndpointAuthMethod, normalizedSecrets);

        var client = new OAuthClient(Guid.NewGuid())
        {
            ClientId = clientId,
            ClientSecrets = normalizedSecrets,
            TokenEndpointAuthMethod = tokenEndpointAuthMethod,
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
        return ClientSecrets.Any(secret => secret.VerifySharedSecret(rawSecret));
    }

    /// <summary>
    /// 返回当前客户端注册的 JWKS。
    /// 主要调用方：private_key_jwt 断言验签。
    /// </summary>
    public string? GetJwks()
    {
        return ClientSecrets
            .FirstOrDefault(secret => string.Equals(secret.Type, OAuthClientSecret.TypeJwks, StringComparison.Ordinal))
            ?.Value;
    }

    public bool RequiresPrivateKeyJwtAuthentication()
    {
        return string.Equals(TokenEndpointAuthMethod, TokenEndpointAuthMethodPrivateKeyJwt, StringComparison.Ordinal);
    }

    public bool AllowsClientSecretAuthentication()
    {
        return string.Equals(TokenEndpointAuthMethod, TokenEndpointAuthMethodClientSecretBasic, StringComparison.Ordinal)
            || string.Equals(TokenEndpointAuthMethod, TokenEndpointAuthMethodClientSecretPost, StringComparison.Ordinal);
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

    private static void ValidateTokenEndpointAuthentication(
        string tokenEndpointAuthMethod,
        IReadOnlyCollection<OAuthClientSecret> clientSecrets)
    {
        if (!string.Equals(tokenEndpointAuthMethod, TokenEndpointAuthMethodClientSecretBasic, StringComparison.Ordinal)
            && !string.Equals(tokenEndpointAuthMethod, TokenEndpointAuthMethodClientSecretPost, StringComparison.Ordinal)
            && !string.Equals(tokenEndpointAuthMethod, TokenEndpointAuthMethodPrivateKeyJwt, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported token_endpoint_auth_method '{tokenEndpointAuthMethod}'.");
        }

        if (string.Equals(tokenEndpointAuthMethod, TokenEndpointAuthMethodPrivateKeyJwt, StringComparison.Ordinal))
        {
            if (!clientSecrets.Any(secret => string.Equals(secret.Type, OAuthClientSecret.TypeJwks, StringComparison.Ordinal)))
                throw new InvalidOperationException("jwks is required for private_key_jwt clients.");

            return;
        }

        if (!clientSecrets.Any(secret => string.Equals(secret.Type, OAuthClientSecret.TypeSharedSecret, StringComparison.Ordinal)))
            throw new InvalidOperationException($"shared_secret is required for {tokenEndpointAuthMethod} clients.");
    }
}
