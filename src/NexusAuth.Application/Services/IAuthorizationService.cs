using Luck.Framework.Infrastructure.DependencyInjectionModule;

namespace NexusAuth.Application.Services;

public interface IAuthorizationService : IScopedDependency
{
    Task<string> GenerateCodeAsync(
        Guid userId,
        string clientId,
        string redirectUri,
        string scope,
        string? codeChallenge = null,
        string? codeChallengeMethod = null,
        CancellationToken ct = default);

    Task<AuthorizationCodeResult> ValidateAndConsumeCodeAsync(
        string code,
        string redirectUri,
        string? codeVerifier = null,
        CancellationToken ct = default);

    Task<ClientCredentialsResult> ValidateClientCredentialsAsync(
        string clientId,
        string rawClientSecret,
        string scope,
        CancellationToken ct = default);
}
