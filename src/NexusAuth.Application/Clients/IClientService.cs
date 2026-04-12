namespace NexusAuth.Application.Clients;

public interface IClientService : IScopedDependency
{
    #region OAuth 授权服务 (Host API 使用)

    Task<OAuthClient> RegisterClientAsync(
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
        CancellationToken ct = default);

    Task<OAuthClient?> ValidateClientAsync(
        string clientId,
        string rawClientSecret,
        CancellationToken ct = default);

    Task<ClientValidationResult> ValidateClientForAuthorizationAsync(
        string clientId,
        string redirectUri,
        string grantType,
        string? codeChallenge = null,
        string? codeChallengeMethod = null,
        CancellationToken ct = default);

    Task<ClientAuthenticationResult> AuthenticateClientAsync(
        string clientId,
        string? rawClientSecret,
        bool requireSecret,
        CancellationToken ct = default);

    Task<ClientAuthenticationResult> AuthenticateClientAsync(
        ClientAuthenticationInput input,
        bool requireClientAuthentication,
        CancellationToken ct = default);

    Task<ClientAuthenticationResult> AuthenticateClientForPostLogoutAsync(
        string clientId,
        string? postLogoutRedirectUri,
        CancellationToken ct = default);

    Task<ScopeValidationResult> ValidateScopesAsync(
        string clientId,
        string scope,
        bool allowIdentityScopes,
        CancellationToken ct = default);

    #endregion

    #region 管理服务 (Workbench 使用)

    Task<List<OAuthClient>> GetAllAsync(CancellationToken ct = default);

    Task<OAuthClient?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<OAuthClient> CreateAsync(CreateClientRequest request, CancellationToken ct = default);

    Task<OAuthClient> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    #endregion
}

public record ClientAuthenticationInput(
    string? ClientId,
    string? ClientSecret,
    string? ClientAssertionType,
    string? ClientAssertion,
    string? AssertionAudience = null);

public record ClientValidationResult(
    bool IsSuccess,
    string? Error,
    string? ErrorCode)
{
    public static ClientValidationResult Success()
        => new(true, null, null);

    public static ClientValidationResult Failure(string errorCode, string error)
        => new(false, error, errorCode);
}

public record ClientAuthenticationResult(
    bool IsSuccess,
    Domain.AggregateRoots.OAuthClients.OAuthClient? Client,
    string? Error,
    string? ErrorCode)
{
    public static ClientAuthenticationResult Success(Domain.AggregateRoots.OAuthClients.OAuthClient client)
        => new(true, client, null, null);

    public static ClientAuthenticationResult Failure(string errorCode, string error)
        => new(false, null, error, errorCode);
}

public record ScopeValidationResult(
    bool IsSuccess,
    string? NormalizedScope,
    string? Error,
    string? ErrorCode)
{
    public static ScopeValidationResult Success(string normalizedScope)
        => new(true, normalizedScope, null, null);

    public static ScopeValidationResult Failure(string errorCode, string error)
        => new(false, null, error, errorCode);
}

public record CreateClientRequest(
    string ClientId,
    string ClientName,
    string? Description,
    List<string>? RedirectUris,
    List<string>? PostLogoutRedirectUris,
    List<string>? AllowedScopes,
    List<string>? AllowedGrantTypes,
    bool RequirePkce,
    string TokenEndpointAuthMethod,
    List<ClientSecretInput>? ClientSecrets,
    List<Guid>? ApiResourceIds);

public record UpdateClientRequest(
    string? ClientName,
    string? Description,
    List<string>? RedirectUris,
    List<string>? PostLogoutRedirectUris,
    List<string>? AllowedScopes,
    List<string>? AllowedGrantTypes,
    bool? RequirePkce,
    bool? IsActive,
    List<ClientSecretInput>? ClientSecrets,
    List<Guid>? ApiResourceIds);

public record ClientSecretInput(
    string Type,
    string Value,
    string? Description);
