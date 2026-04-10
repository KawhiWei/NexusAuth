using Luck.Framework.Infrastructure.DependencyInjectionModule;

namespace NexusAuth.Application.Services.Tokens;

public interface ITokenService : IScopedDependency
{
    Task<string> IssueAccessTokenAsync(
        string clientId,
        string scope,
        string? audience = null,
        Guid? userId = null,
        string? claimsJson = null,
        CancellationToken ct = default);

    Task<TokenIssueResult> IssueAccessTokenWithMetadataAsync(
        string clientId,
        string scope,
        string? audience = null,
        Guid? userId = null,
        string? claimsJson = null,
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
        string? clientId = null,
        CancellationToken ct = default);

    Task RevokeRefreshTokenAsync(
        string refreshTokenString,
        CancellationToken ct = default);

    Task RevokeRefreshTokenAsync(
        string refreshTokenString,
        string? clientId,
        CancellationToken ct = default);

    Task<bool> IsRefreshTokenOwnedByClientAsync(
        string refreshTokenString,
        string clientId,
        CancellationToken ct = default);

    Task RevokeAllUserTokensAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<TokenIntrospectionResult> IntrospectAsync(string token, CancellationToken ct = default);

    Task<TokenIntrospectionResult> IntrospectAsync(string token, string? clientId, CancellationToken ct = default);

    Task RevokeAccessTokenAsync(string accessToken, CancellationToken ct = default);

    Task RevokeAccessTokenAsync(string accessToken, string? clientId, CancellationToken ct = default);
}
