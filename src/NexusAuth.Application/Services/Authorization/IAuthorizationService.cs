using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Application.Services.OIDC;
using NexusAuth.Application.Clients;

namespace NexusAuth.Application.Services.Authorization;

public interface IAuthorizationService : IScopedDependency
{
    Task<string> GenerateCodeAsync(
        Guid userId,
        string clientId,
        string redirectUri,
        string scope,
        string? codeChallenge = null,
        string? codeChallengeMethod = null,
        string? nonce = null,
        string? claimsJson = null,
        DateTimeOffset? authenticatedAt = null,
        string? acr = null,
        string? amr = null,
        CancellationToken ct = default);

    Task<AuthorizationCodeResult> ValidateAndConsumeCodeAsync(
        string code,
        string redirectUri,
        string? codeVerifier = null,
        CancellationToken ct = default);

    Task<ClientCredentialsResult> ValidateClientCredentialsAsync(
        ClientAuthenticationInput authentication,
        string scope,
        CancellationToken ct = default);

    OidcRequestedClaims ParseRequestedClaims(string? claimsJson);
}
