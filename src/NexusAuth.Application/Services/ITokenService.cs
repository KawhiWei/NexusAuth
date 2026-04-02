using Luck.Framework.Infrastructure.DependencyInjectionModule;

namespace NexusAuth.Application.Services;

public interface ITokenService : IScopedDependency
{
    Task<string> IssueAccessTokenAsync(
        string clientId,
        string scope,
        Guid? userId = null,
        CancellationToken ct = default);

    Task<TokenIssueResult> IssueAccessTokenWithMetadataAsync(
        string clientId,
        string scope,
        Guid? userId = null,
        CancellationToken ct = default);

    Task<string> IssueIdTokenAsync(
        string clientId,
        Guid userId,
        string? nonce,
        string accessToken,
        string? claimsJson = null,
        DateTimeOffset? authenticatedAt = null,
        string? acr = null,
        string? amr = null,
        CancellationToken ct = default);

    Task<string> IssueRefreshTokenAsync(
        string clientId,
        Guid userId,
        string scope,
        CancellationToken ct = default);

    Task<RefreshResult> RefreshAsync(
        string refreshTokenString,
        CancellationToken ct = default);

    Task RevokeRefreshTokenAsync(
        string refreshTokenString,
        CancellationToken ct = default);

    Task RevokeAllUserTokensAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<TokenIntrospectionResult> IntrospectAsync(string token, CancellationToken ct = default);

    Task RevokeAccessTokenAsync(string accessToken, CancellationToken ct = default);
}
