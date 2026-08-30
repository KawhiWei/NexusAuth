using NexusAuth.Application.Logging;
using NexusAuth.Application.Services.OIDC;

namespace NexusAuth.Application.Services.Tokens;

public class TokenService(
    IRefreshTokenRepository refreshTokenRepository,
    ITokenBlacklistRepository tokenBlacklistRepository,
    IUserRepository userRepository,
    IApiResourceRepository apiResourceRepository,
    IOptions<JwtOptions> jwtOptions,
    ITokenSigningCredentialsProvider signingCredentialsProvider,
    ILogger<TokenService> logger) : ITokenService
{
    private readonly JwtOptions jwtOptions = jwtOptions.Value;

    /// <summary>
    /// 签发访问令牌（简化返回，仅返�?JWT 字符串）�?
    /// </summary>
    public Task<string> IssueAccessTokenAsync(
        string clientId,
        string scope,
        string? audience = null,
        Guid? userId = null,
        string? claimsJson = null,
        CancellationToken ct = default)
    {
        return IssueAccessTokenWithMetadataAsync(clientId, scope, audience, userId, claimsJson, ct)
            .ContinueWith(t => t.Result.AccessToken, ct);
    }

    /// <summary>
    /// 签发访问令牌并返回元信息（jti、过期时间等）�?
    /// 主要调用方：
    /// - TokenController �?authorization_code / client_credentials / device_code 分支
    /// - TokenService.RefreshAsync 内部续签 access_token
    /// </summary>
    public async Task<TokenIssueResult> IssueAccessTokenWithMetadataAsync(
        string clientId,
        string scope,
        string? audience = null,
        Guid? userId = null,
        string? claimsJson = null,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var jti = Guid.NewGuid().ToString();
        var resolvedAudience = await ResolveAudienceAsync(scope, audience, ct);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId?.ToString() ?? clientId),
            new("client_id", clientId),
            new("scope", scope),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        if (userId.HasValue)
            claims.Add(new("token_use", "access_token"));

        if (!string.IsNullOrWhiteSpace(claimsJson))
        {
            // 中文注释：把原始 OIDC claims 请求透传�?access_token 中，
            // 这样 userinfo 端点可以按客户端真实请求返回更精确的 claim 集合�?
            // 主要调用方：授权码流程与设备码流程换 token�?
            claims.Add(new("claims_json", claimsJson));
        }

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: resolvedAudience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(jwtOptions.AccessTokenLifetimeMinutes),
            signingCredentials: signingCredentialsProvider.GetSigningCredentials());

        token.Header[JwtHeaderParameterNames.Kid] = signingCredentialsProvider.KeyId;

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        using (ApplicationLogScope.Begin(
                   logger,
                   "Token",
                   userId?.ToString() ?? clientId,
                   "AccessTokenIssued"))
        {
            logger.LogInformation(
                "Access token issued. ClientId={ClientId} UserId={UserId} ExpiresAt={ExpiresAt}",
                clientId,
                userId,
                token.ValidTo);
        }

        return await Task.FromResult(new TokenIssueResult(jwt, jti, token.ValidTo));
    }

    /// <summary>
    /// 签发 OIDC �?id_token�?
    /// 主要调用方：TokenController �?authorization_code / device_code 分支�?
    /// </summary>
    public async Task<string> IssueIdTokenAsync(
        string clientId,
        Guid userId,
        string? nonce,
        string accessToken,
        string? claimsJson = null,
        DateTimeOffset? authenticatedAt = null,
        string? acr = null,
        string? amr = null,
        CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(userId, ct)
                   ?? throw new InvalidOperationException("User not found for id_token issuance.");
        var requestedClaims = OidcRequestedClaims.Parse(claimsJson);

        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("client_id", clientId),
            new("token_use", "id_token"),
            new("preferred_username", user.Username),
            new("name", user.Nickname),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("at_hash", ComputeTokenHash(accessToken)),
        };

        // 中文注释：只要是 OIDC 认证结果，auth_time �?RP 做会话时效判断很有价值，直接带上�?
        if (authenticatedAt.HasValue)
            claims.Add(new("auth_time", authenticatedAt.Value.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));

        if (!string.IsNullOrWhiteSpace(acr))
            claims.Add(new("acr", acr));

        if (!string.IsNullOrWhiteSpace(amr))
        {
            foreach (var method in amr.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new("amr", method));
            }
        }

        if (!string.IsNullOrWhiteSpace(user.Email)
            && OidcClaimEmissionPolicy.ShouldEmitRequestedClaim(requestedClaims, "email", user.Email)
            && (requestedClaims.RequestsIdTokenClaim("email") || requestedClaims.RequestsIdTokenClaim("email_verified")))
        {
            claims.Add(new("email", user.Email));
            claims.Add(new("email_verified", "false", ClaimValueTypes.Boolean));
        }

        if (!string.IsNullOrWhiteSpace(user.PhoneNumber)
            && OidcClaimEmissionPolicy.ShouldEmitRequestedClaim(requestedClaims, "phone_number", user.PhoneNumber)
            && (requestedClaims.RequestsIdTokenClaim("phone_number") || requestedClaims.RequestsIdTokenClaim("phone_number_verified")))
        {
            claims.Add(new("phone_number", user.PhoneNumber));
            claims.Add(new("phone_number_verified", "false", ClaimValueTypes.Boolean));
        }

        if (OidcClaimEmissionPolicy.ShouldEmitRequestedClaim(requestedClaims, "gender", user.Gender.ToString()))
            claims.Add(new("gender", user.Gender.ToString()));

        if (!string.IsNullOrWhiteSpace(user.Ethnicity) && OidcClaimEmissionPolicy.ShouldEmitRequestedClaim(requestedClaims, "ethnicity", user.Ethnicity))
            claims.Add(new("ethnicity", user.Ethnicity));

        if (requestedClaims.RequestsIdTokenClaim("nickname"))
            claims.Add(new("nickname", user.Nickname));

        if (!string.IsNullOrWhiteSpace(nonce))
            claims.Add(new("nonce", nonce));

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: clientId,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(jwtOptions.AccessTokenLifetimeMinutes),
            signingCredentials: signingCredentialsProvider.GetSigningCredentials());

        token.Header[JwtHeaderParameterNames.Kid] = signingCredentialsProvider.KeyId;
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        using (ApplicationLogScope.Begin(logger, "Token", userId.ToString(), "IdTokenIssued"))
        {
            logger.LogInformation(
                "ID token issued. ClientId={ClientId} UserId={UserId} ExpiresAt={ExpiresAt}",
                clientId,
                userId,
                token.ValidTo);
        }

        return jwt;
    }

    /// <summary>
    /// 生成并持久化 refresh_token�?
    /// 主要调用方：TokenController �?authorization_code / device_code 分支�?
    /// </summary>
    public async Task<string> IssueRefreshTokenAsync(
        string clientId,
        Guid userId,
        string scope,
        CancellationToken ct = default)
    {
        var refreshLifetime = TimeSpan.FromMinutes(jwtOptions.RefreshTokenLifetimeMinutes);
        var refreshToken = RefreshToken.Create(clientId, userId, scope, refreshLifetime);
        await refreshTokenRepository.AddAsync(refreshToken.Entity, ct);

        using (ApplicationLogScope.Begin(logger, "Token", userId.ToString(), "RefreshTokenIssued"))
        {
            logger.LogInformation(
                "Refresh token issued. ClientId={ClientId} UserId={UserId} ExpiresAt={ExpiresAt}",
                clientId,
                userId,
                refreshToken.Entity.ExpiresAt);
        }

        return refreshToken.RawToken;
    }

    /// <summary>
    /// 使用 refresh_token 轮换刷新访问令牌�?
    /// 主要流程�?
    /// 1. 查找 refresh_token
    /// 2. 校验是否过期、是否已吊销、是否属于当�?client
    /// 3. 吊销�?refresh_token
    /// 4. 签发新的 refresh_token �?access_token
    /// 主要调用方：
    /// - TokenController �?refresh_token 分支
    /// - Demo.Bff 的自动续期逻辑
    /// </summary>
    public async Task<RefreshResult> RefreshAsync(
        string refreshTokenString,
        string? clientId = null,
        CancellationToken ct = default)
    {
        var existingToken = await refreshTokenRepository.FindByTokenAsync(refreshTokenString, ct);

        if (existingToken is null)
            return RefreshFailure(clientId, "RefreshTokenNotFound", "Invalid refresh token.");

        if (existingToken.IsRevoked)
            return RefreshFailure(clientId ?? existingToken.ClientId, "RefreshTokenRevoked", "Refresh token has been revoked.");

        if (existingToken.ExpiresAt <= DateTimeOffset.UtcNow)
            return RefreshFailure(clientId ?? existingToken.ClientId, "RefreshTokenExpired", "Refresh token has expired.");

        var user = await userRepository.FindByIdAsync(existingToken.UserId, ct);
        if (user is null || !user.IsActive)
            return RefreshFailure(clientId ?? existingToken.ClientId, "UserInactive", "The user account is no longer active.");

        // 中文注释：refresh token 必须和当前认证过的客户端绑定，防止一个客户端拿着别人�?refresh token 刷新�?
        // 主要调用方：/connect/token �?refresh_token 分支，以�?Demo.Bff 的会话续期流程�?
        if (!string.IsNullOrWhiteSpace(clientId)
            && !string.Equals(existingToken.ClientId, clientId, StringComparison.Ordinal))
        {
            return RefreshFailure(clientId, "ClientMismatch", "Refresh token does not belong to the authenticated client.");
        }

        // The database transaction atomically revokes the old token and stores
        // the replacement. A concurrent reuse of the same bearer value loses
        // the conditional update and never receives a new token.
        var newRefreshToken = RefreshToken.Create(
            existingToken.ClientId,
            existingToken.UserId,
            existingToken.Scope,
            TimeSpan.FromMinutes(jwtOptions.RefreshTokenLifetimeMinutes));
        var rotatedToken = await refreshTokenRepository.RotateAsync(
            RefreshToken.Hash(refreshTokenString),
            existingToken.ClientId,
            newRefreshToken.Entity,
            ct);
        if (rotatedToken is null)
            return RefreshFailure(existingToken.ClientId, "RefreshTokenRotationFailed", "Refresh token has been revoked, expired, or already used.");

        // Issue new access token
        var accessToken = await IssueAccessTokenAsync(
            existingToken.ClientId,
            existingToken.Scope,
            null,
            existingToken.UserId,
            null,
            ct);

        using (ApplicationLogScope.Begin(logger, "Token", existingToken.ClientId, "RefreshTokenRotated"))
        {
            logger.LogInformation(
                "Refresh token rotation succeeded. ClientId={ClientId} UserId={UserId}",
                existingToken.ClientId,
                existingToken.UserId);
        }

        return RefreshResult.Success(accessToken, newRefreshToken.RawToken);
    }

    /// <summary>
    /// 吊销单个 refresh_token�?
    /// </summary>
    public async Task RevokeRefreshTokenAsync(
        string refreshTokenString,
        CancellationToken ct = default)
    {
        await RevokeRefreshTokenAsync(refreshTokenString, null, ct);
    }

    /// <summary>
    /// 吊销 refresh_token，并可选校�?token 是否属于当前客户端�?
    /// 主要调用方：/connect/revocation �?Demo.Bff 退出登录流程�?
    /// </summary>
    public async Task RevokeRefreshTokenAsync(
        string refreshTokenString,
        string? clientId,
        CancellationToken ct = default)
    {
        var token = await refreshTokenRepository.FindByTokenAsync(refreshTokenString, ct);
        if (token is null)
        {
            using (ApplicationLogScope.Begin(logger, "Token", clientId, "RefreshTokenNotFound"))
            {
                logger.LogDebug("Refresh token revocation skipped. Reason={ReasonCode}", "RefreshTokenNotFound");
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(clientId)
            && !string.Equals(token.ClientId, clientId, StringComparison.Ordinal))
        {
            using (ApplicationLogScope.Begin(logger, "Token", clientId, "ClientMismatch"))
            {
                logger.LogDebug("Refresh token revocation skipped. Reason={ReasonCode}", "ClientMismatch");
            }
            return;
        }

        await refreshTokenRepository.RevokeAsync(token.Id, ct);

        using (ApplicationLogScope.Begin(logger, "Token", token.ClientId, "RefreshTokenRevoked"))
        {
            logger.LogInformation(
                "Refresh token revoked. ClientId={ClientId} UserId={UserId}",
                token.ClientId,
                token.UserId);
        }
    }

    /// <summary>
    /// 判断 refresh_token 是否属于指定客户端�?
    /// 主要调用方：/connect/revocation 的客户端边界检查�?
    /// </summary>
    public async Task<bool> IsRefreshTokenOwnedByClientAsync(
        string refreshTokenString,
        string clientId,
        CancellationToken ct = default)
    {
        var token = await refreshTokenRepository.FindByTokenAsync(refreshTokenString, ct);
        return token is not null && string.Equals(token.ClientId, clientId, StringComparison.Ordinal);
    }

    /// <summary>
    /// 吊销指定用户的全�?refresh_token�?
    /// </summary>
    public async Task RevokeAllUserTokensAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        // Refresh tokens are stateful and are revoked below. Access tokens are JWTs, so
        // record a per-user cutoff that introspection applies to every older token.
        var user = await userRepository.FindByIdAsync(userId, ct);
        if (user is not null)
        {
            user.InvalidateTokens(DateTimeOffset.UtcNow);
            await userRepository.UpdateAsync(user, ct);
        }

        await refreshTokenRepository.RevokeAllForUserAsync(userId, ct);

        using (ApplicationLogScope.Begin(logger, "Token", userId.ToString(), "RefreshTokensRevoked"))
        {
            logger.LogInformation("All refresh tokens revoked for user. UserId={UserId}", userId);
        }
    }

    /// <summary>
    /// �?access_token 进行自检（OAuth2 introspection 风格）�?
    /// </summary>
    public async Task<TokenIntrospectionResult> IntrospectAsync(string token, CancellationToken ct = default)
    {
        return await IntrospectAsync(token, null, ct);
    }

    /// <summary>
    /// �?access_token / id_token 做自检，并可选限制只能由所属客户端查询�?
    /// 主要调用方：/connect/introspect�?connect/userinfo�?
    /// </summary>
    public async Task<TokenIntrospectionResult> IntrospectAsync(string token, string? clientId, CancellationToken ct = default)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
            return TokenIntrospectionResult.Inactive();
        try
        {
            var principal = handler.ValidateToken(
                token,
                signingCredentialsProvider.CreateTokenValidationParameters(jwtOptions.Issuer, null),
                out var validatedToken);

            var jwt = (JwtSecurityToken)validatedToken;
            var jti = GetClaimValue(principal, JwtRegisteredClaimNames.Jti, ClaimTypes.SerialNumber);
            if (!string.IsNullOrWhiteSpace(jti) && await tokenBlacklistRepository.ExistsActiveAsync(jti, DateTimeOffset.UtcNow, ct))
                return TokenIntrospectionResult.Inactive();

            var tokenClientId = principal.FindFirst("client_id")?.Value;
            if (!string.IsNullOrWhiteSpace(clientId)
                && !string.Equals(tokenClientId, clientId, StringComparison.Ordinal))
            {
                return TokenIntrospectionResult.Inactive();
            }

            var subject = GetClaimValue(principal, JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);
            if (Guid.TryParse(subject, out var userId))
            {
                var user = await userRepository.FindByIdAsync(userId, ct);
                if (user is null || !user.IsActive
                    || (user.TokenInvalidBefore.HasValue && jwt.IssuedAt < user.TokenInvalidBefore.Value.UtcDateTime))
                {
                    return TokenIntrospectionResult.Inactive();
                }
            }

            return TokenIntrospectionResult.Success(
                subject,
                tokenClientId,
                principal.FindFirst("scope")?.Value,
                principal.FindFirst("claims_json")?.Value,
                new DateTimeOffset(jwt.ValidTo).ToUnixTimeSeconds(),
                jwt.Issuer,
                principal.FindFirst("token_use")?.Value);
        }
        catch (Exception)
        {
            return TokenIntrospectionResult.Inactive();
        }
    }

    /// <summary>
    /// 吊销 access_token（通过黑名单记�?jti）�?
    /// </summary>
    public async Task RevokeAccessTokenAsync(string accessToken, CancellationToken ct = default)
    {
        await RevokeAccessTokenAsync(accessToken, null, ct);
    }

    /// <summary>
    /// 吊销 access_token，并可选校验它是否属于当前客户端�?
    /// 主要调用方：/connect/revocation�?
    /// </summary>
    public async Task RevokeAccessTokenAsync(string accessToken, string? clientId, CancellationToken ct = default)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(accessToken))
        {
            using (ApplicationLogScope.Begin(logger, "Token", clientId, "TokenNotReadable"))
            {
                logger.LogDebug("Access token revocation skipped. Reason={ReasonCode}", "TokenNotReadable");
            }
            return;
        }

        try
        {
            var principal = handler.ValidateToken(
                accessToken,
                signingCredentialsProvider.CreateTokenValidationParameters(jwtOptions.Issuer, null),
                out var validatedToken);

            var jti = GetClaimValue(principal, JwtRegisteredClaimNames.Jti, ClaimTypes.SerialNumber);
            if (string.IsNullOrWhiteSpace(jti))
            {
                using (ApplicationLogScope.Begin(logger, "Token", clientId, "TokenIdentifierMissing"))
                {
                    logger.LogDebug("Access token revocation skipped. Reason={ReasonCode}", "TokenIdentifierMissing");
                }
                return;
            }

            var tokenClientId = principal.FindFirst("client_id")?.Value;
            if (!string.IsNullOrWhiteSpace(clientId)
                && !string.Equals(tokenClientId, clientId, StringComparison.Ordinal))
            {
                using (ApplicationLogScope.Begin(logger, "Token", clientId, "ClientMismatch"))
                {
                    logger.LogDebug("Access token revocation skipped. Reason={ReasonCode}", "ClientMismatch");
                }
                return;
            }

            var existing = await tokenBlacklistRepository.FindByJtiAsync(jti, ct);
            if (existing is not null)
            {
                using (ApplicationLogScope.Begin(logger, "Token", tokenClientId ?? clientId, "TokenAlreadyRevoked"))
                {
                    logger.LogDebug("Access token revocation skipped. Reason={ReasonCode}", "TokenAlreadyRevoked");
                }
                return;
            }

            var jwt = (JwtSecurityToken)validatedToken;
            var entry = TokenBlacklistEntry.Create(
                jti,
                principal.FindFirst("token_use")?.Value ?? "access_token",
                GetClaimValue(principal, JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier),
                jwt.ValidTo);

            await tokenBlacklistRepository.AddAsync(entry, ct);

            using (ApplicationLogScope.Begin(logger, "Token", tokenClientId ?? clientId, "AccessTokenRevoked"))
            {
                logger.LogInformation(
                    "Access token revoked. ClientId={ClientId} Subject={Subject}",
                    tokenClientId,
                    GetClaimValue(principal, JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier));
            }
        }
        catch
        {
            // OAuth revocation should not leak token validity details.
            using (ApplicationLogScope.Begin(logger, "Token", clientId, "TokenValidationFailed"))
            {
                logger.LogDebug("Access token revocation skipped. Reason={ReasonCode}", "TokenValidationFailed");
            }
        }
    }

    private RefreshResult RefreshFailure(
        string? clientId,
        string reasonCode,
        string error)
    {
        using (ApplicationLogScope.Begin(logger, "Token", clientId, reasonCode))
        {
            logger.LogWarning(
                "Refresh token operation failed. Reason={ReasonCode}",
                reasonCode);
        }

        return RefreshResult.Failure(error);
    }

    private static string ComputeTokenHash(string token)
    {
        var hash = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(token));
        var leftHalf = hash[..(hash.Length / 2)];
        return Base64UrlEncoder.Encode(leftHalf);
    }

    private static string? GetClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private async Task<string> ResolveAudienceAsync(string scope, string? requestedAudience, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(requestedAudience))
            return requestedAudience;

        var scopes = scope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var resourceScopeNames = scopes
            .Where(scopeName => !IsIdentityScope(scopeName))
            .ToArray();

        var resources = await apiResourceRepository.FindByAudiencesAsync(resourceScopeNames, ct);
        var activeAudiences = resources
            .Where(resource => resource.IsActive)
            .Select(resource => resource.Audience)
            .ToHashSet(StringComparer.Ordinal);

        string? resolved = null;
        foreach (var scopeName in resourceScopeNames)
        {
            if (!activeAudiences.Contains(scopeName))
                continue;

            if (string.IsNullOrWhiteSpace(resolved))
            {
                resolved = scopeName;
                continue;
            }

            if (!string.Equals(resolved, scopeName, StringComparison.Ordinal))
                throw new InvalidOperationException("Requested scopes span multiple audiences. Please request one resource audience per token.");
        }

        return resolved ?? jwtOptions.DefaultAudience;
    }

    private static bool IsIdentityScope(string scope)
    {
        return string.Equals(scope, "openid", StringComparison.Ordinal)
            || string.Equals(scope, "profile", StringComparison.Ordinal)
            || string.Equals(scope, "email", StringComparison.Ordinal)
            || string.Equals(scope, "phone", StringComparison.Ordinal)
            || string.Equals(scope, "address", StringComparison.Ordinal)
            || string.Equals(scope, "offline_access", StringComparison.Ordinal);
    }

}

public record TokenIssueResult(string AccessToken, string Jti, DateTime ExpiresAtUtc);

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

public record TokenIntrospectionResult(
    bool Active,
    string? Subject,
    string? ClientId,
    string? Scope,
    string? ClaimsJson,
    long? Exp,
    string? Issuer,
    string? TokenUse)
{
    public static TokenIntrospectionResult Inactive() => new(false, null, null, null, null, null, null, null);

    public static TokenIntrospectionResult Success(string? subject, string? clientId, string? scope, string? claimsJson, long? exp, string? issuer, string? tokenUse)
        => new(true, subject, clientId, scope, claimsJson, exp, issuer, tokenUse);
}
