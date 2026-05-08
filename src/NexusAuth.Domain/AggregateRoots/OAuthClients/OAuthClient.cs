using Luck.DDD.Domain.Domain.AggregateRoots;
using System.Linq;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.AggregateRoots.OAuthClients;

public class OAuthClient : AggregateRootWithIdentity<Guid>
{
    public const string TokenEndpointAuthMethodClientSecretBasic = "client_secret_basic";

    public const string TokenEndpointAuthMethodClientSecretPost = "client_secret_post";

    public const string TokenEndpointAuthMethodClientSecretJwt = "client_secret_jwt";

    public const string TokenEndpointAuthMethodPrivateKeyJwt = "private_key_jwt";

    public const string ClientAssertionTypeJwtBearer = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

    public string ClientId { get; private set; } = default!;

    public List<OAuthClientSecret> ClientSecrets { get; private set; } = [];

    public string TokenEndpointAuthMethod { get; private set; } = TokenEndpointAuthMethodClientSecretBasic;

    public string? Jwks { get; private set; }

    public string? JwksUri { get; private set; }

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
        string? tokenEndpointAuthMethod = null,
        IEnumerable<OAuthClientSecret>? clientSecrets = null,
        string? jwks = null,
        string? jwksUri = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        var normalizedSecrets = clientSecrets?.ToList() ?? [];
        var normalizedAuthMethod = NormalizeTokenEndpointAuthMethod(tokenEndpointAuthMethod);
        ValidateTokenEndpointAuthentication(normalizedAuthMethod, normalizedSecrets, jwks, jwksUri);

        var client = new OAuthClient(id)
        {
            ClientId = clientId,
            ClientSecrets = normalizedSecrets,
            TokenEndpointAuthMethod = normalizedAuthMethod,
            Jwks = NormalizeNullableValue(jwks),
            JwksUri = NormalizeNullableValue(jwksUri),
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
        IEnumerable<OAuthClientSecret>? clientSecrets = null,
        string? jwks = null,
        string? jwksUri = null)
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
            tokenEndpointAuthMethod,
            clientSecrets,
            jwks,
            jwksUri);
    }

    public bool VerifyClientSecret(string rawSecret)
    {
        return ClientSecrets.Any(secret => secret.VerifySharedSecret(rawSecret));
    }

    public IReadOnlyList<string> GetSharedSecretValues()
    {
        return [.. ClientSecrets
            .Where(secret => secret.IsActive && !string.IsNullOrWhiteSpace(secret.PlainValue))
            .Select(secret => secret.PlainValue!)];
    }

    public bool RequiresPrivateKeyJwtAuthentication()
    {
        return string.Equals(TokenEndpointAuthMethod, TokenEndpointAuthMethodPrivateKeyJwt, StringComparison.Ordinal);
    }

    public bool AllowsClientSecretAuthentication()
    {
        return string.Equals(TokenEndpointAuthMethod, TokenEndpointAuthMethodClientSecretBasic, StringComparison.Ordinal)
            || string.Equals(TokenEndpointAuthMethod, TokenEndpointAuthMethodClientSecretPost, StringComparison.Ordinal)
            || string.Equals(TokenEndpointAuthMethod, TokenEndpointAuthMethodClientSecretJwt, StringComparison.Ordinal);
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
        string? tokenEndpointAuthMethod = null,
        IEnumerable<OAuthClientSecret>? clientSecrets = null,
        string? jwks = null,
        string? jwksUri = null)
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

        if (tokenEndpointAuthMethod is { })
            TokenEndpointAuthMethod = NormalizeTokenEndpointAuthMethod(tokenEndpointAuthMethod);

        if (clientSecrets is { } secrets)
            ClientSecrets.AddRange(secrets);

        if (jwks is { })
            Jwks = NormalizeNullableValue(jwks);

        if (jwksUri is { })
            JwksUri = NormalizeNullableValue(jwksUri);

        ValidateTokenEndpointAuthentication(TokenEndpointAuthMethod, ClientSecrets, Jwks, JwksUri);
    }

    public bool AllowsTokenEndpointAuthMethod(string tokenEndpointAuthMethod)
    {
        return string.Equals(TokenEndpointAuthMethod, tokenEndpointAuthMethod, StringComparison.Ordinal);
    }

    public void SetJwks(string? jwks, string? jwksUri = null)
    {
        Jwks = NormalizeNullableValue(jwks);
        JwksUri = NormalizeNullableValue(jwksUri);
        ValidateTokenEndpointAuthentication(TokenEndpointAuthMethod, ClientSecrets, Jwks, JwksUri);
    }

    private static void ValidateTokenEndpointAuthentication(
        string tokenEndpointAuthMethod,
        IReadOnlyCollection<OAuthClientSecret> clientSecrets,
        string? jwks,
        string? jwksUri)
    {
        if (!string.Equals(tokenEndpointAuthMethod, TokenEndpointAuthMethodClientSecretBasic, StringComparison.Ordinal)
            && !string.Equals(tokenEndpointAuthMethod, TokenEndpointAuthMethodClientSecretPost, StringComparison.Ordinal)
            && !string.Equals(tokenEndpointAuthMethod, TokenEndpointAuthMethodClientSecretJwt, StringComparison.Ordinal)
            && !string.Equals(tokenEndpointAuthMethod, TokenEndpointAuthMethodPrivateKeyJwt, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported token_endpoint_auth_method '{tokenEndpointAuthMethod}'.");
        }

        if (string.Equals(tokenEndpointAuthMethod, TokenEndpointAuthMethodPrivateKeyJwt, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(jwks) && string.IsNullOrWhiteSpace(jwksUri))
                return;

            if (!string.IsNullOrWhiteSpace(jwks) && !string.IsNullOrWhiteSpace(jwksUri))
                throw new InvalidOperationException("private_key_jwt client cannot configure both jwks and jwks_uri.");

            return;
        }

        if (string.Equals(tokenEndpointAuthMethod, TokenEndpointAuthMethodClientSecretJwt, StringComparison.Ordinal)
            && clientSecrets.Any(secret => secret.IsActive && string.IsNullOrWhiteSpace(secret.PlainValue)))
        {
            throw new InvalidOperationException("client_secret_jwt requires retrievable shared secret material.");
        }

        if (!string.IsNullOrWhiteSpace(jwks) || !string.IsNullOrWhiteSpace(jwksUri))
            throw new InvalidOperationException($"token_endpoint_auth_method '{tokenEndpointAuthMethod}' does not use jwks or jwks_uri.");
    }

    private static string NormalizeTokenEndpointAuthMethod(string? tokenEndpointAuthMethod)
    {
        return string.IsNullOrWhiteSpace(tokenEndpointAuthMethod)
            ? TokenEndpointAuthMethodClientSecretBasic
            : tokenEndpointAuthMethod.Trim();
    }

    private static string? NormalizeNullableValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
