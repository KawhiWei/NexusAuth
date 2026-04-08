using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Demo.Bff.Models;
using Demo.Bff.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Bff.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public class AuthController : ControllerBase
{
    private readonly ConcurrentDictionary<string, OidcFlowState> _flowStore;
    private readonly OidcBffService _oidcBffService;

    public AuthController(
        ConcurrentDictionary<string, OidcFlowState> flowStore,
        OidcBffService oidcBffService)
    {
        _flowStore = flowStore;
        _oidcBffService = oidcBffService;
    }

    [HttpGet("/api/config")]
    [AllowAnonymous]
    public IActionResult Config()
    {
        return Ok(new
        {
            authority = _oidcBffService.Authority,
            clientId = _oidcBffService.ClientId,
        });
    }

    [HttpGet("/api/auth/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(CancellationToken ct)
    {
        var discovery = await _oidcBffService.FetchDiscoveryAsync(ct);
        var state = Guid.NewGuid().ToString("N");
        var nonce = Guid.NewGuid().ToString("N");
        var pkce = _oidcBffService.GeneratePkce();

        _flowStore[state] = new OidcFlowState(pkce.CodeVerifier, nonce);

        var authorizeUrl = discovery.AuthorizationEndpoint +
                           $"?response_type=code" +
                           $"&client_id={Uri.EscapeDataString(_oidcBffService.ClientId)}" +
                           $"&redirect_uri={Uri.EscapeDataString(_oidcBffService.RedirectUri)}" +
                           $"&scope={Uri.EscapeDataString(_oidcBffService.Scope)}" +
                           $"&state={Uri.EscapeDataString(state)}" +
                           $"&nonce={Uri.EscapeDataString(nonce)}" +
                           $"&code_challenge={Uri.EscapeDataString(pkce.CodeChallenge)}" +
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

        if (!string.IsNullOrWhiteSpace(error))
            return Redirect($"{_oidcBffService.FrontendBaseUrl}/login?error={Uri.EscapeDataString(error)}");

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state) || !_flowStore.TryRemove(state, out var flow))
            return Redirect($"{_oidcBffService.FrontendBaseUrl}/login?error=invalid_callback");

        var discovery = await _oidcBffService.FetchDiscoveryAsync(ct);
        var httpClient = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();
        _oidcBffService.ApplyClientAuthentication(httpClient);
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _oidcBffService.RedirectUri,
            ["code_verifier"] = flow.CodeVerifier,
        });

        var tokenResponse = await httpClient.PostAsync(discovery.TokenEndpoint, tokenRequest, ct);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(ct);
        if (!tokenResponse.IsSuccessStatusCode)
            return Redirect($"{_oidcBffService.FrontendBaseUrl}/login?error=token_exchange_failed");

        using var tokenDocument = JsonDocument.Parse(tokenJson);
        var root = tokenDocument.RootElement;
        var accessToken = root.GetProperty("access_token").GetString();
        var refreshToken = root.TryGetProperty("refresh_token", out var refreshTokenElement) ? refreshTokenElement.GetString() : null;
        var idToken = root.TryGetProperty("id_token", out var idTokenElement) ? idTokenElement.GetString() : null;
        var expiresIn = root.TryGetProperty("expires_in", out var expiresInElement) ? expiresInElement.GetInt32() : 3600;

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(idToken))
            return Redirect($"{_oidcBffService.FrontendBaseUrl}/login?error=missing_tokens");

        var validatedIdToken = await _oidcBffService.ValidateIdTokenAsync(discovery, idToken, flow.Nonce, ct);
        var userInfo = await _oidcBffService.FetchUserInfoAsync(discovery.UserInfoEndpoint, accessToken, ct);

        var claims = new List<Claim>
        {
            new("sub", validatedIdToken.Subject ?? string.Empty),
            new("name", userInfo.Name ?? validatedIdToken.Name ?? validatedIdToken.PreferredUsername ?? validatedIdToken.Subject ?? "Unknown"),
            new("preferred_username", userInfo.PreferredUsername ?? validatedIdToken.PreferredUsername ?? string.Empty),
            new("session_payload", JsonSerializer.Serialize(new SessionPayload(accessToken, refreshToken, idToken, expiresIn, userInfo))),
        };

        if (!string.IsNullOrWhiteSpace(userInfo.Email))
            claims.Add(new("email", userInfo.Email));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
        });

        return Redirect($"{_oidcBffService.FrontendBaseUrl}/auth/callback");
    }

    [HttpGet("/api/auth/me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var session = _oidcBffService.ReadSession(User);
        if (session is null)
            return Unauthorized();

        if (_oidcBffService.IsExpired(session))
        {
            var refreshed = await RefreshSessionAsync(session, ct);
            if (refreshed is null)
                return Unauthorized();

            session = refreshed;
        }

        return Ok(new
        {
            isAuthenticated = true,
            user = session.User,
            expiresIn = session.ExpiresIn,
        });
    }

    [HttpPost("/api/auth/logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var session = _oidcBffService.ReadSession(User);
        if (session is not null)
        {
            var discovery = await _oidcBffService.FetchDiscoveryAsync(ct);
            await _oidcBffService.RevokeIfPresentAsync(discovery.RevocationEndpoint, session.AccessToken, "access_token", ct);
            await _oidcBffService.RevokeIfPresentAsync(discovery.RevocationEndpoint, session.RefreshToken, "refresh_token", ct);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Ok(new
        {
            logoutUrl = $"{_oidcBffService.Authority.TrimEnd('/')}/connect/endsession?id_token_hint={Uri.EscapeDataString(session?.IdToken ?? string.Empty)}&post_logout_redirect_uri={Uri.EscapeDataString(_oidcBffService.PostLogoutRedirectUri)}&state={Uri.EscapeDataString(Guid.NewGuid().ToString("N"))}",
        });
    }

    private async Task<SessionPayload?> RefreshSessionAsync(SessionPayload session, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(session.RefreshToken))
            return null;

        var discovery = await _oidcBffService.FetchDiscoveryAsync(ct);
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();
        _oidcBffService.ApplyClientAuthentication(client);
        var refreshRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = session.RefreshToken,
        });

        var response = await client.PostAsync(discovery.TokenEndpoint, refreshRequest, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = document.RootElement;
        var accessToken = root.GetProperty("access_token").GetString();
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        var refreshed = session with
        {
            AccessToken = accessToken,
            RefreshToken = root.TryGetProperty("refresh_token", out var refreshedRt) ? refreshedRt.GetString() : session.RefreshToken,
            IdToken = root.TryGetProperty("id_token", out var refreshedIdToken) ? refreshedIdToken.GetString() : session.IdToken,
            ExpiresIn = root.TryGetProperty("expires_in", out var expiresIn) ? expiresIn.GetInt32() : session.ExpiresIn,
            IssuedAtUtc = DateTimeOffset.UtcNow,
            User = await _oidcBffService.FetchUserInfoAsync(discovery.UserInfoEndpoint, accessToken, ct),
        };

        var claims = new List<Claim>
        {
            new("sub", refreshed.User.Sub ?? string.Empty),
            new("name", refreshed.User.Name ?? refreshed.User.PreferredUsername ?? refreshed.User.Sub ?? "Unknown"),
            new("preferred_username", refreshed.User.PreferredUsername ?? string.Empty),
            new("session_payload", JsonSerializer.Serialize(refreshed)),
        };

        if (!string.IsNullOrWhiteSpace(refreshed.User.Email))
            claims.Add(new("email", refreshed.User.Email));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
        });

        return refreshed;
    }
}
