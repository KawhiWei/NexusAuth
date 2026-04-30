using Luck.DDD.Domain.Domain.AggregateRoots;
using System.Linq;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.AggregateRoots.OAuthClients;

public class OAuthClient : AggregateRootWithIdentity<Guid>
{
    public const string TokenEndpointAuthMethodClientSecretBasic = "client_secret_basic";

    public const string TokenEndpointAuthMethodClientSecretPost = "client_secret_post";

    public const string TokenEndpointAuthMethodPrivateKeyJwt = "private_key_jwt";

    public const string ClientAssertionTypeJwtBearer = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

    public string ClientId { get; private set; } = default!;

    public List<OAuthClientSecret> ClientSecrets { get; private set; } = [];

    public List<string> TokenEndpointAuthMethods { get; private set; } = [TokenEndpointAuthMethodClientSecretBasic];

    public string TokenEndpointAuthMethod => TokenEndpointAuthMethods.FirstOrDefault() ?? TokenEndpointAuthMethodClientSecretBasic;

    public string ClientName { get; private set; } = default!;

    public string? Description { get; private set; }

    public List<string> RedirectUris { get; private set; } = [];

    public List<string> PostLogoutRedirectUris { get; private set; } = [];

    public List<string> AllowedScopes { get; private set; } = [];

    public List<string> AllowedGrantTypes { get; private set; } = [];

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
        Guid id,
        string clientId,
        string clientName,
        string? description = null,
        IEnumerable<string>? redirectUris = null,
        IEnumerable<string>? postLogoutRedirectUris = null,
        IEnumerable<string>? allowedScopes = null,
        IEnumerable<string>? allowedGrantTypes = null,
        bool requirePkce = true,
        IEnumerable<string>? tokenEndpointAuthMethods = null,
        IEnumerable<OAuthClientSecret>? clientSecrets = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        var normalizedSecrets = clientSecrets?.ToList() ?? [];
        var normalizedAuthMethods = NormalizeTokenEndpointAuthMethods(tokenEndpointAuthMethods);
        ValidateTokenEndpointAuthentication(normalizedAuthMethods, normalizedSecrets);

        var client = new OAuthClient(id)
        {
            ClientId = clientId,
            ClientSecrets = normalizedSecrets,
            TokenEndpointAuthMethods = normalizedAuthMethods,
            ClientName = clientName,
            Description = description,
            RedirectUris = redirectUris?.ToList() ?? [],
            PostLogoutRedirectUris = postLogoutRedirectUris?.ToList() ?? [],
            AllowedScopes = allowedScopes?.ToList() ?? [],
            AllowedGrantTypes = allowedGrantTypes?.ToList() ?? [],
            RequirePkce = requirePkce,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return client;
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
        return Create(
            Guid.NewGuid(),
            clientId,
            clientName,
            description,
            redirectUris,
            postLogoutRedirectUris,
            allowedScopes,
            allowedGrantTypes,
            requirePkce,
            [tokenEndpointAuthMethod],
            clientSecrets);
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
        IEnumerable<string>? tokenEndpointAuthMethods = null,
        IEnumerable<OAuthClientSecret>? clientSecrets = null)
    {
        return Create(
            Guid.NewGuid(),
            clientId,
            clientName,
            description,
            redirectUris,
            postLogoutRedirectUris,
            allowedScopes,
            allowedGrantTypes,
            requirePkce,
            tokenEndpointAuthMethods,
            clientSecrets);
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
            .FirstOrDefault(secret => secret.IsActive && string.Equals(secret.Type, OAuthClientSecret.TypeJwks, StringComparison.Ordinal))
            ?.Value;
    }

    public IReadOnlyList<string> GetJwksValues()
    {
        return [.. ClientSecrets
            .Where(secret => secret.IsActive && string.Equals(secret.Type, OAuthClientSecret.TypeJwks, StringComparison.Ordinal))
            .Select(secret => secret.Value)];
    }

    public bool RequiresPrivateKeyJwtAuthentication()
    {
        return TokenEndpointAuthMethods.Contains(TokenEndpointAuthMethodPrivateKeyJwt, StringComparer.Ordinal);
    }

    public bool AllowsClientSecretAuthentication()
    {
        return TokenEndpointAuthMethods.Contains(TokenEndpointAuthMethodClientSecretBasic, StringComparer.Ordinal)
            || TokenEndpointAuthMethods.Contains(TokenEndpointAuthMethodClientSecretPost, StringComparer.Ordinal);
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

    public void Update(
        string? clientName = null,
        string? description = null,
        IEnumerable<string>? redirectUris = null,
        IEnumerable<string>? postLogoutRedirectUris = null,
        IEnumerable<string>? allowedScopes = null,
        IEnumerable<string>? allowedGrantTypes = null,
        bool? requirePkce = null,
        bool? isActive = null,
        IEnumerable<string>? tokenEndpointAuthMethods = null,
        IEnumerable<OAuthClientSecret>? clientSecrets = null)
    {
        if (!string.IsNullOrWhiteSpace(clientName))
            ClientName = clientName;
        
        if (description is { } || clientName is { })
            Description = description;
        
        if (redirectUris is { })
            RedirectUris = [.. redirectUris];
        
        if (postLogoutRedirectUris is { })
            PostLogoutRedirectUris = [.. postLogoutRedirectUris];
        
        if (allowedScopes is { })
            AllowedScopes = [.. allowedScopes];
        
        if (allowedGrantTypes is { })
            AllowedGrantTypes = [.. allowedGrantTypes];
        
        if (requirePkce is { } requirePkceValue)
            RequirePkce = requirePkceValue;
        
        if (isActive is { } isActiveValue)
            IsActive = isActiveValue;

        if (tokenEndpointAuthMethods is { })
            TokenEndpointAuthMethods = NormalizeTokenEndpointAuthMethods(tokenEndpointAuthMethods);
        
        if (clientSecrets is { } secrets)
            ClientSecrets.AddRange(secrets);

        ValidateTokenEndpointAuthentication(TokenEndpointAuthMethods, ClientSecrets);
    }

    public bool AllowsTokenEndpointAuthMethod(string tokenEndpointAuthMethod)
    {
        return TokenEndpointAuthMethods.Contains(tokenEndpointAuthMethod, StringComparer.Ordinal);
    }

    private static List<string> NormalizeTokenEndpointAuthMethods(IEnumerable<string>? tokenEndpointAuthMethods)
    {
        var methods = tokenEndpointAuthMethods?
            .Where(method => !string.IsNullOrWhiteSpace(method))
            .Select(method => method.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];

        return methods.Count == 0 ? [TokenEndpointAuthMethodClientSecretBasic] : methods;
    }

    private static void ValidateTokenEndpointAuthentication(
        IReadOnlyCollection<string> tokenEndpointAuthMethods,
        IReadOnlyCollection<OAuthClientSecret> clientSecrets)
    {
        foreach (var tokenEndpointAuthMethod in tokenEndpointAuthMethods)
        {
            if (!string.Equals(tokenEndpointAuthMethod, TokenEndpointAuthMethodClientSecretBasic, StringComparison.Ordinal)
                && !string.Equals(tokenEndpointAuthMethod, TokenEndpointAuthMethodClientSecretPost, StringComparison.Ordinal)
                && !string.Equals(tokenEndpointAuthMethod, TokenEndpointAuthMethodPrivateKeyJwt, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsupported token_endpoint_auth_method '{tokenEndpointAuthMethod}'.");
            }
        }
    }
}
