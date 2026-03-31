using Luck.Framework.Infrastructure.DependencyInjectionModule;

namespace NexusAuth.Application.Services;

public interface ITokenService : IScopedDependency
{
    Task<string> IssueAccessTokenAsync(
        string clientId,
        string scope,
        Guid? userId = null,
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
}
