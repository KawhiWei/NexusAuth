using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using NexusAuth.Application.Services;

namespace NexusAuth.Host.Controllers;

[ApiController]
public class AuthorizeController(
    IAuthorizationService authorizationService,
    IClientService clientService)
    : ControllerBase
{
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
    /// <summary>
    /// OAuth2/OIDC 授权端点，负责校验请求并签发 authorization code。
    /// 主要流程：
    /// 1. 校验 client_id / redirect_uri / scope / PKCE
    /// 2. 检查当前浏览器是否已登录
    /// 3. 必要时跳转登录页或 consent 页
    /// 4. 登录成功后签发 authorization code 并重定向回客户端
    /// 主要调用方：
    /// - Demo.Bff
    /// - Demo.Bff.ClientSecret
    /// - 任意标准 OIDC Web Client
    /// </summary>
    /// <exception cref="ArgumentNullException"></exception>
    [HttpGet("/connect/authorize")]
    public async Task<IActionResult> Authorize(
        [FromQuery(Name = "response_type")] string responseType,
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "redirect_uri")] string redirectUri,
        [FromQuery] string scope,
        [FromQuery] string? state = null,
        [FromQuery] string? nonce = null,
        [FromQuery] string? prompt = null,
        [FromQuery(Name = "max_age")] int? maxAge = null,
        [FromQuery] string? claims = null,
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
        var clientValidation = await clientService.ValidateClientForAuthorizationAsync(
            clientId, redirectUri, "authorization_code", codeChallenge, codeChallengeMethod, ct);

        if (!clientValidation.IsSuccess)
            return BadRequest(new { error = clientValidation.ErrorCode, error_description = clientValidation.Error });

        if (!string.IsNullOrWhiteSpace(claims))
        {
            try
            {
                // 中文注释：OIDC claims 参数允许客户端声明希望返回哪些 claim，这里先做格式校验。
                authorizationService.ParseRequestedClaims(claims);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = "invalid_request", error_description = ex.Message });
            }
        }

        var promptValues = ParsePrompt(prompt);
        if (promptValues.Contains("none", StringComparer.Ordinal) && promptValues.Count > 1)
            return BadRequest(new { error = "invalid_request", error_description = "prompt=none cannot be combined with other prompt values." });

        // Check if user is already authenticated via cookie
        if (User.Identity?.IsAuthenticated != true)
        {
            if (promptValues.Contains("none", StringComparer.Ordinal))
                return Redirect(BuildErrorRedirectUri(redirectUri, "login_required", "User authentication is required.", state));

            // Not logged in — redirect to login page with returnUrl (must be relative for Url.IsLocalUrl)
            var returnUrl = Request.GetEncodedPathAndQuery();
            return Redirect($"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        if (promptValues.Contains("login", StringComparer.Ordinal))
        {
            // 中文注释：prompt=login 要求强制用户重新登录，即使当前已经有 Cookie 会话。
            await HttpContext.SignOutAsync(AppWebModule.AuthenticationScheme);
            var returnUrl = Request.GetEncodedPathAndQuery();
            return Redirect($"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        if (promptValues.Contains("consent", StringComparer.Ordinal))
        {
            // 中文注释：当 RP 显式要求 prompt=consent 时，先进入服务端确认页，
            // 由当前用户确认本次授权的 scope 与 claims，再回到授权端继续签发 code。
            // 主要调用方：标准 OIDC 客户端，以及后续需要强制重新确认授权的 demo 场景。
            return Redirect(BuildConsentPageUrl(clientId, redirectUri, scope, state, nonce, prompt, maxAge, claims, codeChallenge, codeChallengeMethod));
        }

        // User is authenticated — extract user ID from cookie claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return BadRequest(new { error = "server_error", error_description = "Unable to identify the authenticated user." });

        var authenticatedAt = GetAuthenticatedAt();
        if (maxAge.HasValue)
        {
            if (!authenticatedAt.HasValue || DateTimeOffset.UtcNow > authenticatedAt.Value.AddSeconds(maxAge.Value))
            {
                if (promptValues.Contains("none", StringComparer.Ordinal))
                    return Redirect(BuildErrorRedirectUri(redirectUri, "login_required", "The current session is too old for max_age.", state));

                await HttpContext.SignOutAsync(AppWebModule.AuthenticationScheme);
                var returnUrl = Request.GetEncodedPathAndQuery();
                return Redirect($"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
            }
        }

        // Generate authorization code
        var code = await authorizationService.GenerateCodeAsync(
            userId,
            clientId,
            redirectUri,
            scope,
            codeChallenge,
            codeChallengeMethod,
            nonce,
            claims,
            authenticatedAt,
            User.FindFirst("acr")?.Value,
            User.FindFirst("amr")?.Value,
            ct);

        // Build redirect URI with code (and state if present)
        var separator = redirectUri.Contains('?') ? '&' : '?';
        var redirectUrl = $"{redirectUri}{separator}code={Uri.EscapeDataString(code)}";
        if (!string.IsNullOrWhiteSpace(state))
            redirectUrl += $"&state={Uri.EscapeDataString(state)}";

        return Redirect(redirectUrl);
    }

    private DateTimeOffset? GetAuthenticatedAt()
    {
        var authTimeClaim = User.FindFirst("auth_time")?.Value;
        if (long.TryParse(authTimeClaim, out var unixTimeSeconds))
            return DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);

        return null;
    }

    private static HashSet<string> ParsePrompt(string? prompt)
    {
        return prompt?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal)
            ?? [];
    }

    private static string BuildErrorRedirectUri(string redirectUri, string error, string description, string? state)
    {
        var separator = redirectUri.Contains('?') ? '&' : '?';
        var errorRedirect = $"{redirectUri}{separator}error={Uri.EscapeDataString(error)}&error_description={Uri.EscapeDataString(description)}";
        if (!string.IsNullOrWhiteSpace(state))
            errorRedirect += $"&state={Uri.EscapeDataString(state)}";

        return errorRedirect;
    }

    private static string BuildConsentPageUrl(
        string clientId,
        string redirectUri,
        string scope,
        string? state,
        string? nonce,
        string? prompt,
        int? maxAge,
        string? claims,
        string? codeChallenge,
        string? codeChallengeMethod)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = scope,
            ["state"] = state,
            ["nonce"] = nonce,
            ["prompt"] = prompt,
            ["max_age"] = maxAge?.ToString(),
            ["claims"] = claims,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = codeChallengeMethod,
        };

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/consent", query!);
    }
}
