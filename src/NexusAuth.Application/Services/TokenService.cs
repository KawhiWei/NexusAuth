using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Application.Services;

public class TokenService : ITokenService
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly JwtOptions _jwtOptions;

    public TokenService(
        IRefreshTokenRepository refreshTokenRepository,
        IOptions<JwtOptions> jwtOptions)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _jwtOptions = jwtOptions.Value;
    }

    public Task<string> IssueAccessTokenAsync(
        string clientId,
        string scope,
        Guid? userId = null,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId?.ToString() ?? clientId),
            new("client_id", clientId),
            new("scope", scope),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_jwtOptions.AccessTokenLifetimeMinutes),
            signingCredentials: credentials);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return Task.FromResult(jwt);
    }

    public async Task<string> IssueRefreshTokenAsync(
        string clientId,
        Guid userId,
        string scope,
        CancellationToken ct = default)
    {
        var refreshToken = RefreshToken.Create(clientId, userId, scope);
        await _refreshTokenRepository.AddAsync(refreshToken, ct);

        return refreshToken.Token;
    }

    public async Task<RefreshResult> RefreshAsync(
        string refreshTokenString,
        CancellationToken ct = default)
    {
        var existingToken = await _refreshTokenRepository.FindByTokenAsync(refreshTokenString, ct);

        if (existingToken is null)
            return RefreshResult.Failure("Invalid refresh token.");

        if (existingToken.IsRevoked)
            return RefreshResult.Failure("Refresh token has been revoked.");

        if (existingToken.ExpiresAt <= DateTimeOffset.UtcNow)
            return RefreshResult.Failure("Refresh token has expired.");

        // Revoke old token
        await _refreshTokenRepository.RevokeAsync(existingToken.Id, ct);

        // Issue new refresh token
        var newRefreshToken = RefreshToken.Create(
            existingToken.ClientId,
            existingToken.UserId,
            existingToken.Scope);
        await _refreshTokenRepository.AddAsync(newRefreshToken, ct);

        // Issue new access token
        var accessToken = await IssueAccessTokenAsync(
            existingToken.ClientId,
            existingToken.Scope,
            existingToken.UserId,
            ct);

        return RefreshResult.Success(accessToken, newRefreshToken.Token);
    }

    public async Task RevokeRefreshTokenAsync(
        string refreshTokenString,
        CancellationToken ct = default)
    {
        var token = await _refreshTokenRepository.FindByTokenAsync(refreshTokenString, ct);
        if (token is not null)
            await _refreshTokenRepository.RevokeAsync(token.Id, ct);
    }

    public async Task RevokeAllUserTokensAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        await _refreshTokenRepository.RevokeAllForUserAsync(userId, ct);
    }
}

public record RefreshResult(
    bool IsSuccess,
    string? AccessToken,
    string? RefreshToken,
    string? Error)
{
    public static RefreshResult Success(string accessToken, string refreshToken)
        => new(true, accessToken, refreshToken, null);

    public static RefreshResult Failure(string error)
        => new(false, null, null, error);
}
