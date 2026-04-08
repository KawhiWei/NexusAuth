using NexusAuth.Domain.AggregateRoots.OAuthClients;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Application.Services;

public class ClientService : IClientService
{
    private readonly IOAuthClientRepository _clientRepository;
    private readonly IApiResourceRepository _apiResourceRepository;

    public ClientService(
        IOAuthClientRepository clientRepository,
        IApiResourceRepository apiResourceRepository)
    {
        _clientRepository = clientRepository;
        _apiResourceRepository = apiResourceRepository;
    }

    /// <summary>
    /// 注册 OAuth 客户端。
    /// </summary>
    public async Task<OAuthClient> RegisterClientAsync(
        string clientId,
        string rawClientSecret,
        string clientName,
        string? description = null,
        IEnumerable<string>? redirectUris = null,
        IEnumerable<string>? postLogoutRedirectUris = null,
        IEnumerable<string>? allowedScopes = null,
        IEnumerable<string>? allowedGrantTypes = null,
        bool requirePkce = true,
        CancellationToken ct = default)
    {
        var existing = await _clientRepository.FindByClientIdAsync(clientId, ct);
        if (existing is not null)
            throw new InvalidOperationException($"ClientId '{clientId}' is already registered.");

        var client = OAuthClient.Create(
            clientId,
            rawClientSecret,
            clientName,
            description,
            redirectUris,
            postLogoutRedirectUris,
            allowedScopes,
            allowedGrantTypes,
            requirePkce);

        await _clientRepository.AddAsync(client, ct);

        return client;
    }

    /// <summary>
    /// 使用 client_id + client_secret 校验客户端。
    /// </summary>
    public async Task<OAuthClient?> ValidateClientAsync(
        string clientId,
        string rawClientSecret,
        CancellationToken ct = default)
    {
        var client = await _clientRepository.FindByClientIdAsync(clientId, ct);

        if (client is null || !client.IsActive)
            return null;

        return client.VerifyClientSecret(rawClientSecret) ? client : null;
    }

    /// <summary>
    /// 校验授权端请求中的客户端参数、回调地址、授权类型与 PKCE 要求。
    /// </summary>
    public async Task<ClientValidationResult> ValidateClientForAuthorizationAsync(
        string clientId,
        string redirectUri,
        string grantType,
        string? codeChallenge = null,
        CancellationToken ct = default)
    {
        var client = await _clientRepository.FindByClientIdAsync(clientId, ct);

        if (client is null || !client.IsActive)
            return ClientValidationResult.Failure("invalid_client", "Client not found or inactive.");

        if (!client.IsValidRedirectUri(redirectUri))
            return ClientValidationResult.Failure("invalid_request", "Invalid redirect_uri.");

        if (!client.IsGrantTypeAllowed(grantType))
            return ClientValidationResult.Failure("unauthorized_client",
                $"Client is not allowed to use {grantType} grant type.");

        if (client.RequirePkce && string.IsNullOrWhiteSpace(codeChallenge))
            return ClientValidationResult.Failure("invalid_request",
                "code_challenge is required for this client (PKCE).");

        return ClientValidationResult.Success();
    }

    /// <summary>
    /// 统一的客户端认证入口（可配置是否必须校验 client_secret）。
    /// </summary>
    public async Task<ClientAuthenticationResult> AuthenticateClientAsync(
        string clientId,
        string? rawClientSecret,
        bool requireSecret,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return ClientAuthenticationResult.Failure("invalid_client", "client_id is required.");

        var client = await _clientRepository.FindByClientIdAsync(clientId, ct);
        if (client is null || !client.IsActive)
            return ClientAuthenticationResult.Failure("invalid_client", "Client not found or inactive.");

        if (requireSecret)
        {
            if (string.IsNullOrWhiteSpace(rawClientSecret))
                return ClientAuthenticationResult.Failure("invalid_client", "client_secret is required.");

            if (!client.VerifyClientSecret(rawClientSecret))
                return ClientAuthenticationResult.Failure("invalid_client", "Invalid client secret.");
        }

        return ClientAuthenticationResult.Success(client);
    }

    /// <summary>
    /// 用于 OIDC logout 场景校验 RP 身份和 post_logout_redirect_uri 白名单。
    /// 主要调用方：Host 层的 /connect/endsession 端点。
    /// </summary>
    public async Task<ClientAuthenticationResult> AuthenticateClientForPostLogoutAsync(
        string clientId,
        string? postLogoutRedirectUri,
        CancellationToken ct = default)
    {
        var client = await _clientRepository.FindByClientIdAsync(clientId, ct);
        if (client is null || !client.IsActive)
            return ClientAuthenticationResult.Failure("invalid_client", "Client not found or inactive.");

        if (!string.IsNullOrWhiteSpace(postLogoutRedirectUri)
            && !client.IsValidPostLogoutRedirectUri(postLogoutRedirectUri))
        {
            return ClientAuthenticationResult.Failure("invalid_request", "post_logout_redirect_uri is not registered for this client.");
        }

        return ClientAuthenticationResult.Success(client);
    }

    /// <summary>
    /// 校验 scope 是否被客户端允许，且资源是否有效。
    /// </summary>
    public async Task<ScopeValidationResult> ValidateScopesAsync(
        string clientId,
        string scope,
        bool allowIdentityScopes,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return ScopeValidationResult.Failure("invalid_scope", "scope is required.");

        var client = await _clientRepository.FindByClientIdAsync(clientId, ct);
        if (client is null || !client.IsActive)
            return ScopeValidationResult.Failure("invalid_client", "Client not found or inactive.");

        var requestedScopes = scope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var requestedScope in requestedScopes)
        {
            if (!client.AllowedScopes.Contains(requestedScope, StringComparer.Ordinal))
                return ScopeValidationResult.Failure("invalid_scope", $"Scope '{requestedScope}' is not allowed for this client.");

            if (allowIdentityScopes && IsIdentityScope(requestedScope))
                continue;

            var resource = await _apiResourceRepository.FindByNameAsync(requestedScope, ct);
            if (resource is null || !resource.IsActive)
                return ScopeValidationResult.Failure("invalid_scope", $"Scope '{requestedScope}' does not correspond to an active resource.");
        }

        return ScopeValidationResult.Success(string.Join(' ', requestedScopes));
    }

    private static bool IsIdentityScope(string scope)
    {
        return string.Equals(scope, "openid", StringComparison.Ordinal)
            || string.Equals(scope, "profile", StringComparison.Ordinal)
            || string.Equals(scope, "email", StringComparison.Ordinal)
            || string.Equals(scope, "phone", StringComparison.Ordinal)
            || string.Equals(scope, "address", StringComparison.Ordinal)
            || string.Equals(scope, "offline_access", StringComparison.Ordinal);
    }
}
