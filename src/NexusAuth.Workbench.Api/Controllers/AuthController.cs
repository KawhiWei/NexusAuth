using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusAuth.Extension;

namespace NexusAuth.Workbench.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IFlowStateStore _flowStore;
    private readonly IOidcWorkbenchService _oidcService;

    public AuthController(IFlowStateStore flowStore, IOidcWorkbenchService oidcService)
    {
        _flowStore = flowStore;
        _oidcService = oidcService;
    }

    [HttpGet("/api/auth/config")]
    [AllowAnonymous]
    public IActionResult Config()
    {
        return Ok(new
        {
            authority = _oidcService.Authority,
            clientId = _oidcService.ClientId,
            redirectUri = _oidcService.RedirectUri,
        });
    }

    [HttpGet("/api/auth/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(CancellationToken ct)
    {
        var discovery = await _oidcService.FetchDiscoveryAsync(ct);

        var state = Guid.NewGuid().ToString("N");
        var nonce = Guid.NewGuid().ToString("N");
        string codeChallenge;
        string codeVerifier;
        (codeChallenge, codeVerifier) = _oidcService.GeneratePkce();

        await _flowStore.AddAsync(state, new FlowState(codeVerifier, nonce), ct);

        var authorizeUrl = discovery.AuthorizationEndpoint +
            $"?response_type=code" +
            $"&client_id={Uri.EscapeDataString(_oidcService.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(_oidcService.RedirectUri)}" +
            $"&scope={Uri.EscapeDataString(_oidcService.Scope)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&nonce={Uri.EscapeDataString(nonce)}" +
            $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
            $"&code_challenge_method=S256";

        return Ok(new { authorizeUrl });
    }

    [HttpGet("/signin-oidc")]
    [AllowAnonymous]
    public async Task<IActionResult> SignInOidc(CancellationToken ct)
    {
        var code = Request.Query["code"].ToString();
        var state = Request.Query["state"].ToString();
        var error = Request.Query["error"].ToString();

        var frontendBase = _oidcService.PostLogoutRedirectUri.TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(error))
            return Redirect($"{frontendBase}/login?error={Uri.EscapeDataString(error)}");

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return Redirect($"{frontendBase}/login?error=invalid_callback");

        var flow = await _flowStore.GetAsync(state, ct);
        if (flow == null)
            return Redirect($"{frontendBase}/login?error=invalid_callback");

        await _flowStore.RemoveAsync(state, ct);

        try
        {
            string accessToken;
            string idToken;
            int expiresIn;
            (accessToken, idToken, expiresIn) = await _oidcService.ExchangeCodeAsync(code, flow.CodeVerifier, ct);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, idToken),
                new(ClaimTypes.Name, idToken),
                new("access_token", accessToken),
                new("id_token", idToken),
            };

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24),
            });

            return Redirect($"{frontendBase}/auth/callback");
        }
        catch
        {
            return Redirect($"{frontendBase}/login?error=token_exchange_failed");
        }
    }

    [HttpGet("/api/auth/me")]
    [AllowAnonymous]
    public IActionResult Me()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { isAuthenticated = false });

        return Ok(new
        {
            isAuthenticated = true,
            user = new
            {
                id = User.FindFirstValue(ClaimTypes.NameIdentifier),
                name = User.Identity.Name
            }
        });
    }

    [HttpPost("/api/auth/logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var logoutUrl = string.Empty;
        var idToken = User.FindFirstValue("id_token");

        if (_oidcService.SignOutProvider && !string.IsNullOrWhiteSpace(idToken))
        {
            logoutUrl = $"{_oidcService.Authority.TrimEnd('/')}/connect/endsession" +
                $"?id_token_hint={Uri.EscapeDataString(idToken)}" +
                $"&post_logout_redirect_uri={Uri.EscapeDataString(_oidcService.PostLogoutRedirectUri)}";
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            Response.Cookies.Append(".NexusAuth.Workbench", "", new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                Expires = DateTimeOffset.UtcNow.AddDays(-1)
            });
        }

        return Ok(new { logoutUrl });
    }
}