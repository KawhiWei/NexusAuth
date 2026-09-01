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
        [FromQuery(Name = "response_type")] string? responseType,
        [FromQuery(Name = "client_id")] string? clientId,
        [FromQuery(Name = "redirect_uri")] string? redirectUri,
        [FromQuery] string? scope,
        [FromQuery] string? state = null,
        [FromQuery] string? nonce = null,
        [FromQuery] string? prompt = null,
        [FromQuery(Name = "max_age")] int? maxAge = null,
        [FromQuery(Name = "response_mode")] string? responseMode = null,
        [FromQuery] string? claims = null,
        [FromQuery(Name = "code_challenge")] string? codeChallenge = null,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return AuthorizationError("invalid_request", "client_id is required.");

        if (string.IsNullOrWhiteSpace(redirectUri))
            return AuthorizationError("invalid_request", "redirect_uri is required.");

        // Establish the trusted callback boundary before any redirect. Invalid client or
        // redirect_uri requests are always handled locally and never become open redirects.
        var redirectValidation = await clientService.ValidateClientRedirectUriAsync(clientId, redirectUri, ct);

        if (!redirectValidation.IsSuccess)
        {
            return AuthorizationError(
                redirectValidation.ErrorCode ?? "invalid_request",
                redirectValidation.Error ?? "The authorization request is invalid.");
        }

        var normalizedResponseMode = ResolveResponseMode(responseMode);
        if (normalizedResponseMode is null)
        {
            return AuthorizationError(
                redirectUri,
                "query",
                "invalid_request",
                "response_mode must be query or form_post.",
                state);
        }

        if (string.IsNullOrWhiteSpace(responseType))
        {
            return AuthorizationError(
                redirectUri,
                normalizedResponseMode,
                "invalid_request",
                "response_type is required.",
                state);
        }

        if (!string.Equals(responseType, "code", StringComparison.Ordinal))
        {
            return AuthorizationError(
                redirectUri,
                normalizedResponseMode,
                "unsupported_response_type",
                "Only 'code' response type is supported.",
                state);
        }

        var clientValidation = await clientService.ValidateClientForAuthorizationAsync(
            clientId, redirectUri, "authorization_code", codeChallenge, codeChallengeMethod, ct);

        if (!clientValidation.IsSuccess)
        {
            return AuthorizationError(
                redirectUri,
                normalizedResponseMode,
                clientValidation.ErrorCode ?? "invalid_request",
                clientValidation.Error ?? "The authorization request is invalid.",
                state);
        }

        if (string.IsNullOrWhiteSpace(scope))
        {
            return AuthorizationError(
                redirectUri,
                normalizedResponseMode,
                "invalid_request",
                "scope is required.",
                state);
        }

        var scopeValidation = await clientService.ValidateScopesAsync(clientId, scope, allowIdentityScopes: true, ct);
        if (!scopeValidation.IsSuccess)
        {
            if (string.Equals(scopeValidation.ErrorCode, "invalid_client", StringComparison.Ordinal))
                return AuthorizationError("invalid_client", scopeValidation.Error ?? "Client not found or inactive.");

            return AuthorizationError(
                redirectUri,
                normalizedResponseMode,
                scopeValidation.ErrorCode ?? "invalid_scope",
                scopeValidation.Error ?? "The requested scope is invalid.",
                state);
        }

        var promptValues = ParsePrompt(prompt);
        var unsupportedPrompt = promptValues.FirstOrDefault(value =>
            !value.Equals("none", StringComparison.Ordinal)
            && !value.Equals("login", StringComparison.Ordinal)
            && !value.Equals("consent", StringComparison.Ordinal));
        if (unsupportedPrompt is not null)
        {
            return AuthorizationError(
                redirectUri,
                normalizedResponseMode,
                "invalid_request",
                $"Unsupported prompt value '{unsupportedPrompt}'.",
                state);
        }

        if (maxAge is < 0)
        {
            return AuthorizationError(
                redirectUri,
                normalizedResponseMode,
                "invalid_request",
                "max_age must not be negative.",
                state);
        }

        if (!string.IsNullOrWhiteSpace(claims))
        {
            try
            {
                // 中文注释：OIDC claims 参数允许客户端声明希望返回哪些 claim，这里先做格式校验。
                authorizationService.ParseRequestedClaims(claims);
            }
            catch (InvalidOperationException ex)
            {
                return AuthorizationError(redirectUri, normalizedResponseMode, "invalid_request", ex.Message, state);
            }
        }

        if (promptValues.Contains("none", StringComparer.Ordinal) && promptValues.Count > 1)
        {
            return AuthorizationError(
                redirectUri,
                normalizedResponseMode,
                "invalid_request",
                "prompt=none cannot be combined with other prompt values.",
                state);
        }

        // Check if user is already authenticated via cookie
        if (User.Identity?.IsAuthenticated != true)
        {
            if (promptValues.Contains("none", StringComparer.Ordinal))
            {
                return AuthorizationError(
                    redirectUri,
                    normalizedResponseMode,
                    "login_required",
                    "User authentication is required.",
                    state);
            }

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
            return Redirect(BuildConsentPageUrl(clientId, redirectUri, scope, state, nonce, prompt, maxAge, responseMode, claims, codeChallenge, codeChallengeMethod));
        }

        // User is authenticated — extract user ID from cookie claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return AuthorizationError("server_error", "Unable to identify the authenticated user.");

        var authenticatedAt = GetAuthenticatedAt();
        if (maxAge.HasValue)
        {
            if (!authenticatedAt.HasValue || DateTimeOffset.UtcNow > authenticatedAt.Value.AddSeconds(maxAge.Value))
            {
                if (promptValues.Contains("none", StringComparer.Ordinal))
                {
                    return AuthorizationError(
                        redirectUri,
                        normalizedResponseMode,
                        "login_required",
                        "The current session is too old for max_age.",
                        state);
                }

                await HttpContext.SignOutAsync(AppWebModule.AuthenticationScheme);
                var returnUrl = Request.GetEncodedPathAndQuery();
                return Redirect($"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
            }
        }

        // Generate authorization code
        string code;
        try
        {
            code = await authorizationService.GenerateCodeAsync(
                userId,
                clientId,
                redirectUri,
                scopeValidation.NormalizedScope!,
                codeChallenge,
                codeChallengeMethod,
                nonce,
                claims,
                authenticatedAt,
                User.FindFirst("acr")?.Value,
                User.FindFirst("amr")?.Value,
                ct);
        }
        catch (InvalidOperationException)
        {
            // Scope and claims were validated above. A concurrent client change can still
            // invalidate them between validation and persistence; return an OAuth error
            // instead of leaking a framework 500 response.
            return AuthorizationError(
                redirectUri,
                normalizedResponseMode,
                "invalid_scope",
                "The requested scope is no longer valid.",
                state);
        }

        var parameters = new Dictionary<string, string?>
        {
            ["code"] = code,
            ["state"] = state,
        };

        return BuildAuthorizationResponse(redirectUri, normalizedResponseMode, parameters);
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

    private static string? ResolveResponseMode(string? responseMode)
    {
        if (string.IsNullOrWhiteSpace(responseMode)
            || string.Equals(responseMode, "query", StringComparison.Ordinal))
            return "query";

        return string.Equals(responseMode, "form_post", StringComparison.Ordinal)
            ? "form_post"
            : null;
    }

    private IActionResult AuthorizationError(
        string error,
        string description)
    {
        return RedirectToPage("/OAuthError", new
        {
            error,
            error_description = description,
        });
    }

    private IActionResult AuthorizationError(
        string redirectUri,
        string? responseMode,
        string error,
        string description,
        string? state)
    {
        return AuthorizationError(error, description);
    }

    private IActionResult BuildAuthorizationResponse(
        string redirectUri,
        string responseMode,
        IReadOnlyDictionary<string, string?> parameters)
    {
        var presentParameters = parameters
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.Ordinal);

        if (!string.Equals(responseMode, "form_post", StringComparison.Ordinal))
        {
            var redirectUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
                redirectUri,
                presentParameters);
            return new RedirectResult(redirectUrl);
        }

        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["Pragma"] = "no-cache";
        var action = System.Net.WebUtility.HtmlEncode(redirectUri);
        var hiddenInputs = string.Join(
            Environment.NewLine,
            presentParameters.Select(pair =>
                $"<input type=\"hidden\" name=\"{System.Net.WebUtility.HtmlEncode(pair.Key)}\" value=\"{System.Net.WebUtility.HtmlEncode(pair.Value)}\" />"));
        var html = $"<!doctype html><html><head><meta charset=\"utf-8\"><title>Submitting authorization response</title></head><body><form method=\"post\" action=\"{action}\">{hiddenInputs}<noscript><button type=\"submit\">Continue</button></noscript></form><script>document.forms[0].submit();</script></body></html>";
        return new ContentResult
        {
            Content = html,
            ContentType = "text/html; charset=utf-8",
            StatusCode = StatusCodes.Status200OK,
        };
    }

    private static string BuildConsentPageUrl(
        string clientId,
        string redirectUri,
        string scope,
        string? state,
        string? nonce,
        string? prompt,
        int? maxAge,
        string? responseMode,
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
            ["response_mode"] = responseMode,
            ["claims"] = claims,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = codeChallengeMethod,
        };

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/consent", query!);
    }
}
