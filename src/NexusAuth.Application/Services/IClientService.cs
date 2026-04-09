using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.AggregateRoots.OAuthClients;

namespace NexusAuth.Application.Services;

public interface IClientService : IScopedDependency
{
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

    /// <summary>
    /// Validates client for authorization code flow: existence, active status,
    /// redirect URI, grant type, and PKCE requirements.
    /// </summary>
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
