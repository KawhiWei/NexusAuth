using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.AggregateRoots.OAuthClients;

namespace NexusAuth.Application.Services;

public interface IClientService : IScopedDependency
{
    Task<OAuthClient> RegisterClientAsync(
        string clientId,
        string rawClientSecret,
        string clientName,
        string? description = null,
        IEnumerable<string>? redirectUris = null,
        IEnumerable<string>? allowedScopes = null,
        IEnumerable<string>? allowedGrantTypes = null,
        bool requirePkce = true,
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
        CancellationToken ct = default);
}

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
