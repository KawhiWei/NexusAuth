using NexusAuth.Application;
using NexusAuth.Domain.Entities;

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

    Task<ClientValidationResult> ValidateClientRedirectUriAsync(
        string clientId,
        string redirectUri,
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

    Task<List<ClientDto>> GetAllAsync(string? keyword = null, bool? isActive = null, CancellationToken ct = default);

    Task<PagedResult<ClientDto>> GetPagedAsync(
        string? keyword = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default);

    Task<ClientDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<ClientMutationResultDto> CreateAsync(CreateClientRequest request, CancellationToken ct = default);

    Task<ClientDto> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default);

    Task<ClientMutationResultDto> GenerateCredentialAsync(Guid id, GenerateClientCredentialRequest request, CancellationToken ct = default);

    Task<ClientMutationResultDto> ResetCredentialAsync(Guid id, GenerateClientCredentialRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    #endregion
}

public record ClientAuthenticationInput(
    string? ClientId,
    string? ClientSecret,
    string? ClientAssertionType,
    string? ClientAssertion,
    string? AssertionAudience = null,
    string? TokenEndpointAuthMethod = null);

public record ClientAuthenticationParseResult(
    bool IsSuccess,
    ClientAuthenticationInput? Authentication,
    string? Error,
    string? ErrorCode)
{
    public static ClientAuthenticationParseResult Success(ClientAuthenticationInput authentication)
        => new(true, authentication, null, null);

    public static ClientAuthenticationParseResult Failure(string error = "Invalid client authentication.")
        => new(false, null, error, "invalid_client");
}

public record ClientValidationResult(
    bool IsSuccess,
    string? Error,
    string? ErrorCode,
    bool RedirectUriValidated = false)
{
    public static ClientValidationResult Success()
        => new(true, null, null, true);

    public static ClientValidationResult Failure(string errorCode, string error, bool redirectUriValidated = false)
        => new(false, error, errorCode, redirectUriValidated);
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
    string? TokenEndpointAuthMethod,
    bool? AutoGenerateJwks,
    string? Jwks,
    string? JwksUri,
    List<Guid>? ApiResourceIds = null);

public record UpdateClientRequest(
    string? ClientName,
    string? Description,
    List<string>? RedirectUris,
    List<string>? PostLogoutRedirectUris,
    List<string>? AllowedScopes,
    List<string>? AllowedGrantTypes,
    bool? RequirePkce,
    string? TokenEndpointAuthMethod,
    string? Jwks,
    string? JwksUri,
    bool? IsActive,
    List<Guid>? ApiResourceIds = null);

public record GenerateClientCredentialRequest(
    string? TokenEndpointAuthMethod = null,
    bool? AutoGenerateJwks = null,
    string? Description = null);

public record ClientDto(
    Guid Id,
    string ClientId,
    List<ClientCredentialDto> Credentials,
    string TokenEndpointAuthMethod,
    string? Jwks,
    string? JwksUri,
    string ClientName,
    string? Description,
    List<string> RedirectUris,
    List<string> PostLogoutRedirectUris,
    List<string> AllowedScopes,
    List<string> AllowedGrantTypes,
    bool RequirePkce,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public List<Guid> ApiResourceIds { get; init; } = [];
}

public record ClientCredentialDto(
    Guid Id,
    string Type,
    bool IsActive,
    DateTimeOffset CreatedAt);

public record GeneratedClientCredentialDto(
    string Type,
    string? ClientSecret,
    string? PrivateKeyPem,
    string? Jwks,
    string? Description);

public record ClientMutationResultDto(
    ClientDto Client,
    GeneratedClientCredentialDto? GeneratedCredential);
