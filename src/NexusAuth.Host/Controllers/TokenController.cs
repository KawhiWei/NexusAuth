using Microsoft.AspNetCore.Mvc;
using NexusAuth.Application.Services;

namespace NexusAuth.Host.Controllers;

[ApiController]
public class TokenController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITokenService _tokenService;
    private readonly IDeviceAuthorizationService _deviceAuthorizationService;
    private readonly IClientService _clientService;

    public TokenController(
        IAuthorizationService authorizationService,
        ITokenService tokenService,
        IDeviceAuthorizationService deviceAuthorizationService,
        IClientService clientService)
    {
        _authorizationService = authorizationService;
        _tokenService = tokenService;
        _deviceAuthorizationService = deviceAuthorizationService;
        _clientService = clientService;
    }

    /// <summary>
    /// OAuth2.0 Token Endpoint.
    /// Supports grant_type: authorization_code, client_credentials, refresh_token.
    /// </summary>
    /// <summary>
    /// 统一 Token 入口，根据 grant_type 分发到具体流程。
    /// </summary>
    [HttpPost("/connect/token")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token(
        [FromForm(Name = "grant_type")] string grantType,
        [FromForm(Name = "client_id")] string? clientId = null,
        [FromForm(Name = "client_secret")] string? clientSecret = null,
        [FromForm] string? code = null,
        [FromForm(Name = "redirect_uri")] string? redirectUri = null,
        [FromForm(Name = "code_verifier")] string? codeVerifier = null,
        [FromForm] string? scope = null,
        [FromForm(Name = "refresh_token")] string? refreshToken = null,
        [FromForm(Name = "device_code")] string? deviceCode = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(grantType))
            return BadRequest(new { error = "invalid_request", error_description = "grant_type is required." });

        // 中文注释：统一解析 OAuth2 客户端认证，优先兼容标准的 client_secret_basic，
        // 同时保留现有 demo 在用的 client_secret_post 表单方式。
        // 主要调用方：Demo.Bff、移动端 demo，以及任意标准 OAuth2/OIDC 客户端。
        var resolvedClientAuthentication = OAuthClientAuthenticationParser.ResolveClientAuthentication(
            Request.Headers.Authorization.ToString(),
            clientId,
            clientSecret);
        clientId = resolvedClientAuthentication.ClientId;
        clientSecret = resolvedClientAuthentication.ClientSecret;

        return grantType switch
        {
            "authorization_code" => await HandleAuthorizationCodeAsync(clientId, clientSecret, code, redirectUri, codeVerifier, ct),
            "client_credentials" => await HandleClientCredentialsAsync(clientId, clientSecret, scope, ct),
            "refresh_token" => await HandleRefreshTokenAsync(clientId, clientSecret, refreshToken, ct),
            "urn:ietf:params:oauth:grant-type:device_code" => await HandleDeviceCodeAsync(clientId, clientSecret, deviceCode, ct),
            _ => BadRequest(new { error = "unsupported_grant_type", error_description = $"Grant type '{grantType}' is not supported." }),
        };
    }

    private async Task<IActionResult> HandleAuthorizationCodeAsync(
        string? clientId,
        string? clientSecret,
        string? code,
        string? redirectUri,
        string? codeVerifier,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "invalid_request", error_description = "code is required." });

        if (string.IsNullOrWhiteSpace(redirectUri))
            return BadRequest(new { error = "invalid_request", error_description = "redirect_uri is required." });

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            var authentication = await _clientService.AuthenticateClientAsync(clientId, clientSecret, requireSecret: true, ct);
            if (!authentication.IsSuccess)
                return Unauthorized(new { error = authentication.ErrorCode ?? "invalid_client", error_description = authentication.Error });
        }

        var result = await _authorizationService.ValidateAndConsumeCodeAsync(code, redirectUri, codeVerifier, ct);

        if (!result.IsSuccess)
            return BadRequest(new { error = "invalid_grant", error_description = result.Error });

        var accessTokenResult = await _tokenService.IssueAccessTokenWithMetadataAsync(result.ClientId, result.Scope, null, result.UserId, result.ClaimsJson, ct);
        var refreshToken = ShouldIssueRefreshToken(result.Scope)
            ? await _tokenService.IssueRefreshTokenAsync(result.ClientId, result.UserId, result.Scope, ct)
            : null;
        var includeIdToken = result.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("openid", StringComparer.Ordinal);
        string? idToken = null;
        if (includeIdToken)
        {
            // 中文注释：授权码里已经保存了 OIDC 上下文，这里在换 token 时继续带到 id_token 里。
            idToken = await _tokenService.IssueIdTokenAsync(
                result.ClientId,
                result.UserId,
                result.Nonce,
                accessTokenResult.AccessToken,
                result.ClaimsJson,
                result.AuthenticatedAt,
                result.Acr,
                result.Amr,
                ct);
        }

        return Ok(new
        {
            access_token = accessTokenResult.AccessToken,
            token_type = "Bearer",
            refresh_token = refreshToken,
            scope = result.Scope,
            id_token = idToken,
            expires_in = 3600,
        });
    }

    private async Task<IActionResult> HandleClientCredentialsAsync(
        string? clientId,
        string? clientSecret,
        string? scope,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return BadRequest(new { error = "invalid_request", error_description = "client_id is required." });

        if (string.IsNullOrWhiteSpace(clientSecret))
            return BadRequest(new { error = "invalid_request", error_description = "client_secret is required." });

        if (string.IsNullOrWhiteSpace(scope))
            return BadRequest(new { error = "invalid_request", error_description = "scope is required." });

        var result = await _authorizationService.ValidateClientCredentialsAsync(clientId, clientSecret, scope, ct);

        if (!result.IsSuccess)
            return BadRequest(new { error = "invalid_client", error_description = result.Error });

        var accessToken = await _tokenService.IssueAccessTokenAsync(result.ClientId, result.Scope, null, null, null, ct);

        return Ok(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            scope = result.Scope,
        });
    }

    private async Task<IActionResult> HandleRefreshTokenAsync(
        string? clientId,
        string? clientSecret,
        string? refreshToken,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return BadRequest(new { error = "invalid_request", error_description = "refresh_token is required." });

        if (string.IsNullOrWhiteSpace(clientId))
            return BadRequest(new { error = "invalid_request", error_description = "client_id is required." });

        var authentication = await _clientService.AuthenticateClientAsync(clientId, clientSecret, requireSecret: true, ct);
        if (!authentication.IsSuccess)
            return Unauthorized(new { error = authentication.ErrorCode ?? "invalid_client", error_description = authentication.Error });

        var result = await _tokenService.RefreshAsync(refreshToken, clientId, ct);

        if (!result.IsSuccess)
            return BadRequest(new { error = "invalid_grant", error_description = result.Error });

        return Ok(new
        {
            access_token = result.AccessToken,
            token_type = "Bearer",
            refresh_token = result.RefreshToken,
            expires_in = 3600,
        });
    }

    private async Task<IActionResult> HandleDeviceCodeAsync(
        string? clientId,
        string? clientSecret,
        string? deviceCode,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return BadRequest(new { error = "invalid_request", error_description = "client_id is required." });

        if (string.IsNullOrWhiteSpace(deviceCode))
            return BadRequest(new { error = "invalid_request", error_description = "device_code is required." });

        var result = await _deviceAuthorizationService.PollAsync(clientId, clientSecret, deviceCode, ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode is "authorization_pending" or "slow_down" or "access_denied" or "expired_token")
                return BadRequest(new { error = result.ErrorCode, error_description = result.Error, interval = result.Interval > 0 ? (int?)result.Interval : null });

            return BadRequest(new { error = "invalid_grant", error_description = result.Error });
        }

        var accessTokenResult = await _tokenService.IssueAccessTokenWithMetadataAsync(result.ClientId!, result.Scope!, null, result.UserId, null, ct);
        var refreshToken = ShouldIssueRefreshToken(result.Scope!)
            ? await _tokenService.IssueRefreshTokenAsync(result.ClientId!, result.UserId, result.Scope!, ct)
            : null;
        var includeIdToken = result.Scope!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("openid", StringComparer.Ordinal);
        string? idToken = null;
        if (includeIdToken)
            idToken = await _tokenService.IssueIdTokenAsync(result.ClientId!, result.UserId, null, accessTokenResult.AccessToken, ct: ct);

        return Ok(new
        {
            access_token = accessTokenResult.AccessToken,
            token_type = "Bearer",
            refresh_token = refreshToken,
            scope = result.Scope,
            id_token = idToken,
            expires_in = 3600,
        });
    }

    private static bool ShouldIssueRefreshToken(string scope)
    {
        // 中文注释：refresh token 只在请求了 offline_access 时才签发，
        // 这样 discovery 文档里的 OIDC 能力声明和真实行为保持一致。
        // 主要调用方：authorization_code 与 device_code 的换 token 流程。
        return scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("offline_access", StringComparer.Ordinal);
    }
}
