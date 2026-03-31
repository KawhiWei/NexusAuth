using System.Security.Claims;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using NexusAuth.Application.Services;

namespace NexusAuth.Host.Controllers;

[ApiController]
public class AuthorizeController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IClientService _clientService;

    public AuthorizeController(
        IAuthorizationService authorizationService,
        IClientService clientService)
    {
        _authorizationService = authorizationService;
        _clientService = clientService;
    }

    /// <summary>
    /// OAuth2.0 Authorization Endpoint.
    /// Supports response_type=code (Authorization Code flow with optional PKCE).
    ///
    /// If the user is already authenticated (via cookie), the authorization code is issued
    /// immediately and the user is redirected back to the client — no login page shown.
    ///
    /// If the user is NOT authenticated, they are redirected to /account/login with the
    /// current URL as returnUrl. After login, they are redirected back here with a valid cookie.
    /// </summary>
    [HttpGet("/connect/authorize")]
    public async Task<IActionResult> Authorize(
        [FromQuery(Name = "response_type")] string responseType,
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "redirect_uri")] string redirectUri,
        [FromQuery] string scope,
        [FromQuery] string? state = null,
        [FromQuery(Name = "code_challenge")] string? codeChallenge = null,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod = null,
        CancellationToken ct = default)
    {
        // Only authorization_code flow is supported
        if (responseType != "code")
            return BadRequest(new { error = "unsupported_response_type", error_description = "Only 'code' response type is supported." });

        if (string.IsNullOrWhiteSpace(clientId))
            return BadRequest(new { error = "invalid_request", error_description = "client_id is required." });

        if (string.IsNullOrWhiteSpace(redirectUri))
            return BadRequest(new { error = "invalid_request", error_description = "redirect_uri is required." });

        if (string.IsNullOrWhiteSpace(scope))
            return BadRequest(new { error = "invalid_request", error_description = "scope is required." });

        // Validate client via Application layer
        var clientValidation = await _clientService.ValidateClientForAuthorizationAsync(
            clientId, redirectUri, "authorization_code", codeChallenge, ct);

        if (!clientValidation.IsSuccess)
            return BadRequest(new { error = clientValidation.ErrorCode, error_description = clientValidation.Error });

        // Check if user is already authenticated via cookie
        if (User.Identity?.IsAuthenticated != true)
        {
            // Not logged in — redirect to login page with returnUrl (must be relative for Url.IsLocalUrl)
            var returnUrl = Request.GetEncodedPathAndQuery();
            return Redirect($"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        // User is authenticated — extract user ID from cookie claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return BadRequest(new { error = "server_error", error_description = "Unable to identify the authenticated user." });

        // Generate authorization code
        var code = await _authorizationService.GenerateCodeAsync(
            userId,
            clientId,
            redirectUri,
            scope,
            codeChallenge,
            codeChallengeMethod,
            ct);

        // Build redirect URI with code (and state if present)
        var separator = redirectUri.Contains('?') ? '&' : '?';
        var redirectUrl = $"{redirectUri}{separator}code={Uri.EscapeDataString(code)}";
        if (!string.IsNullOrWhiteSpace(state))
            redirectUrl += $"&state={Uri.EscapeDataString(state)}";

        return Redirect(redirectUrl);
    }
}
