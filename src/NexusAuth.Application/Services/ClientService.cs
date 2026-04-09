using NexusAuth.Domain.AggregateRoots.OAuthClients;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Application.Services;

public class ClientService : IClientService
{
    private readonly IOAuthClientRepository _clientRepository;
    private readonly IApiResourceRepository _apiResourceRepository;
    private readonly ITokenBlacklistRepository _tokenBlacklistRepository;

    public ClientService(
        IOAuthClientRepository clientRepository,
        IApiResourceRepository apiResourceRepository,
        ITokenBlacklistRepository tokenBlacklistRepository)
    {
        _clientRepository = clientRepository;
        _apiResourceRepository = apiResourceRepository;
        _tokenBlacklistRepository = tokenBlacklistRepository;
    }

    /// <summary>
    /// 注册 OAuth 客户端。
    /// </summary>
    public async Task<OAuthClient> RegisterClientAsync(
        string clientId,
        string clientName,
        string? description = null,
        IEnumerable<string>? redirectUris = null,
        IEnumerable<string>? postLogoutRedirectUris = null,
        IEnumerable<string>? allowedScopes = null,
        IEnumerable<string>? allowedGrantTypes = null,
        bool requirePkce = true,
        string tokenEndpointAuthMethod = OAuthClient.TokenEndpointAuthMethodClientSecretBasic,
        IEnumerable<OAuthClientSecret>? clientSecrets = null,
        CancellationToken ct = default)
    {
        var existing = await _clientRepository.FindByClientIdAsync(clientId, ct);
        if (existing is not null)
            throw new InvalidOperationException($"ClientId '{clientId}' is already registered.");

        var client = OAuthClient.Create(
            clientId,
            clientName,
            description,
            redirectUris,
            postLogoutRedirectUris,
            allowedScopes,
            allowedGrantTypes,
            requirePkce,
            tokenEndpointAuthMethod,
            clientSecrets);

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

    public async Task<ClientAuthenticationResult> AuthenticateClientAsync(
        ClientAuthenticationInput input,
        bool requireClientAuthentication,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.ClientId))
            return ClientAuthenticationResult.Failure("invalid_client", "client_id is required.");

        var client = await _clientRepository.FindByClientIdAsync(input.ClientId, ct);
        if (client is null || !client.IsActive)
            return ClientAuthenticationResult.Failure("invalid_client", "Client not found or inactive.");

        if (!requireClientAuthentication)
            return ClientAuthenticationResult.Success(client);

        if (client.RequiresPrivateKeyJwtAuthentication())
        {
            if (!string.Equals(input.ClientAssertionType, OAuthClient.ClientAssertionTypeJwtBearer, StringComparison.Ordinal))
            {
                return ClientAuthenticationResult.Failure("invalid_client", "client_assertion_type is invalid for this client.");
            }

            if (string.IsNullOrWhiteSpace(input.ClientAssertion))
                return ClientAuthenticationResult.Failure("invalid_client", "client_assertion is required.");

            if (string.IsNullOrWhiteSpace(input.AssertionAudience))
                return ClientAuthenticationResult.Failure("invalid_client", "assertion audience is required for private_key_jwt validation.");

            var assertionValidation = ClientPrivateKeyJwtValidator.Validate(input.ClientAssertion, client, input.AssertionAudience);
            if (!assertionValidation.IsSuccess)
                return ClientAuthenticationResult.Failure("invalid_client", assertionValidation.Error ?? "Invalid client assertion.");

            // 中文注释：private_key_jwt 必须防止 assertion 在有效期内被重复使用，
            // 这里复用现有 token_blacklist_entries 记录 jti，作为一次性断言缓存。
            if (string.IsNullOrWhiteSpace(assertionValidation.Jti) || assertionValidation.ExpiresAt is null)
                return ClientAuthenticationResult.Failure("invalid_client", "client_assertion metadata is incomplete.");

            var replayKey = BuildClientAssertionReplayKey(client.ClientId, assertionValidation.Jti);
            if (await _tokenBlacklistRepository.ExistsActiveAsync(replayKey, DateTimeOffset.UtcNow, ct))
                return ClientAuthenticationResult.Failure("invalid_client", "client_assertion has already been used.");

            await _tokenBlacklistRepository.AddAsync(
                TokenBlacklistEntry.Create(replayKey, "client_assertion", client.ClientId, assertionValidation.ExpiresAt.Value),
                ct);

            return ClientAuthenticationResult.Success(client);
        }

        if (string.IsNullOrWhiteSpace(input.ClientSecret))
            return ClientAuthenticationResult.Failure("invalid_client", "client_secret is required.");

        if (!client.AllowsClientSecretAuthentication())
            return ClientAuthenticationResult.Failure("invalid_client", "Client does not allow client_secret authentication.");

        if (!client.VerifyClientSecret(input.ClientSecret))
            return ClientAuthenticationResult.Failure("invalid_client", "Invalid client secret.");

        return ClientAuthenticationResult.Success(client);
    }

    /// <summary>
    /// 校验授权端请求中的客户端参数、回调地址、授权类型与 PKCE 要求。
    /// </summary>
    public async Task<ClientValidationResult> ValidateClientForAuthorizationAsync(
        string clientId,
        string redirectUri,
        string grantType,
        string? codeChallenge = null,
        string? codeChallengeMethod = null,
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

        if (string.Equals(grantType, "authorization_code", StringComparison.OrdinalIgnoreCase))
        {
            // 中文注释：向 OAuth 2.1 收敛时，authorization_code 流程统一强制 PKCE，
            // 并且只允许更安全的 S256，不再接受 plain。
            // 主要调用方：/connect/authorize。
            if (string.IsNullOrWhiteSpace(codeChallenge))
                return ClientValidationResult.Failure("invalid_request", "code_challenge is required for authorization_code flow.");

            if (!string.Equals(codeChallengeMethod, "S256", StringComparison.Ordinal))
                return ClientValidationResult.Failure("invalid_request", "code_challenge_method must be S256 for authorization_code flow.");
        }

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
        var input = new ClientAuthenticationInput(clientId, rawClientSecret, null, null, null);
        return await AuthenticateClientAsync(input, requireSecret, ct);
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

    private static string BuildClientAssertionReplayKey(string clientId, string assertionJti)
    {
        return $"client_assertion:{clientId}:{assertionJti}";
    }
}
