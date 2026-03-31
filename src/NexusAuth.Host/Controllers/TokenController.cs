using Microsoft.AspNetCore.Mvc;
using NexusAuth.Application.Services;

namespace NexusAuth.Host.Controllers;

[ApiController]
public class TokenController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITokenService _tokenService;

    public TokenController(
        IAuthorizationService authorizationService,
        ITokenService tokenService)
    {
        _authorizationService = authorizationService;
        _tokenService = tokenService;
    }

    /// <summary>
    /// OAuth2.0 Token Endpoint.
    /// Supports grant_type: authorization_code, client_credentials, refresh_token.
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
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(grantType))
            return BadRequest(new { error = "invalid_request", error_description = "grant_type is required." });

        return grantType switch
        {
            "authorization_code" => await HandleAuthorizationCodeAsync(code, redirectUri, codeVerifier, ct),
            "client_credentials" => await HandleClientCredentialsAsync(clientId, clientSecret, scope, ct),
            "refresh_token" => await HandleRefreshTokenAsync(refreshToken, ct),
            _ => BadRequest(new { error = "unsupported_grant_type", error_description = $"Grant type '{grantType}' is not supported." }),
        };
    }

    private async Task<IActionResult> HandleAuthorizationCodeAsync(
        string? code,
        string? redirectUri,
        string? codeVerifier,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "invalid_request", error_description = "code is required." });

        if (string.IsNullOrWhiteSpace(redirectUri))
            return BadRequest(new { error = "invalid_request", error_description = "redirect_uri is required." });

        var result = await _authorizationService.ValidateAndConsumeCodeAsync(code, redirectUri, codeVerifier, ct);

        if (!result.IsSuccess)
            return BadRequest(new { error = "invalid_grant", error_description = result.Error });

        var accessToken = await _tokenService.IssueAccessTokenAsync(result.ClientId, result.Scope, result.UserId, ct);
        var refreshToken = await _tokenService.IssueRefreshTokenAsync(result.ClientId, result.UserId, result.Scope, ct);

        return Ok(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            refresh_token = refreshToken,
            scope = result.Scope,
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

        var accessToken = await _tokenService.IssueAccessTokenAsync(result.ClientId, result.Scope, ct: ct);

        return Ok(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            scope = result.Scope,
        });
    }

    private async Task<IActionResult> HandleRefreshTokenAsync(
        string? refreshToken,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return BadRequest(new { error = "invalid_request", error_description = "refresh_token is required." });

        var result = await _tokenService.RefreshAsync(refreshToken, ct);

        if (!result.IsSuccess)
            return BadRequest(new { error = "invalid_grant", error_description = result.Error });

        return Ok(new
        {
            access_token = result.AccessToken,
            token_type = "Bearer",
            refresh_token = result.RefreshToken,
        });
    }
}
