using Demo.Bff.Options;
using Demo.Bff.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;

namespace Demo.Bff.Controllers;

[ApiController]
public class MobileAuthController(
    OidcBffService oidcBffService,
    IOptions<MobileAuthOptions> mobileAuthOptions)
    : ControllerBase
{
    private readonly MobileAuthOptions _mobileAuthOptions = mobileAuthOptions.Value;

    [HttpGet("/api/mobile/auth/config")]
    [AllowAnonymous]
    public IActionResult Config()
    {
        return Ok(new
        {
            authority = oidcBffService.Authority,
            clientId = _mobileAuthOptions.ClientId,
            redirectUri = _mobileAuthOptions.RedirectUri,
            scope = _mobileAuthOptions.Scope,
            tokenEndpoint = $"{oidcBffService.Authority.TrimEnd('/')}/connect/token",
            authorizeEndpoint = $"{oidcBffService.Authority.TrimEnd('/')}/connect/authorize",
        });
    }

    [HttpGet("/api/mobile/auth/authorize-url")]
    [AllowAnonymous]
    public async Task<IActionResult> AuthorizeUrl([FromQuery] string? state = null, [FromQuery] string? nonce = null)
    {
        var discovery = await oidcBffService.FetchDiscoveryAsync(HttpContext.RequestAborted);
        var actualState = string.IsNullOrWhiteSpace(state) ? Guid.NewGuid().ToString("N") : state;
        var actualNonce = string.IsNullOrWhiteSpace(nonce) ? Guid.NewGuid().ToString("N") : nonce;
        var pkce = oidcBffService.GeneratePkce();

        var authorizeUrl = discovery.AuthorizationEndpoint +
                           $"?response_type=code" +
                           $"&client_id={Uri.EscapeDataString(_mobileAuthOptions.ClientId)}" +
                           $"&redirect_uri={Uri.EscapeDataString(_mobileAuthOptions.RedirectUri)}" +
                           $"&scope={Uri.EscapeDataString(_mobileAuthOptions.Scope)}" +
                           $"&state={Uri.EscapeDataString(actualState)}" +
                           $"&nonce={Uri.EscapeDataString(actualNonce)}" +
                           $"&code_challenge={Uri.EscapeDataString(pkce.CodeChallenge)}" +
                           $"&code_challenge_method=S256";

        // 中文注释：移动端通常自己持有 code_verifier，这里返回给客户端存储。
        return Ok(new
        {
            authorizeUrl,
            state = actualState,
            nonce = actualNonce,
            codeVerifier = pkce.CodeVerifier,
            codeChallenge = pkce.CodeChallenge,
        });
    }

    [HttpPost("/api/mobile/auth/token")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> ExchangeCode(
        [FromForm] string code,
        [FromForm(Name = "code_verifier")] string codeVerifier,
        [FromForm(Name = "redirect_uri")] string? redirectUri = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(codeVerifier))
            return BadRequest(new { error = "invalid_request", error_description = "code and code_verifier are required." });

        var discovery = await oidcBffService.FetchDiscoveryAsync(ct);
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();

        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _mobileAuthOptions.ClientId,
            ["client_secret"] = _mobileAuthOptions.ClientSecret,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = redirectUri ?? _mobileAuthOptions.RedirectUri,
        });

        var response = await client.PostAsync(discovery.TokenEndpoint, request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        return Content(payload, "application/json", Encoding.UTF8);
    }

    [HttpPost("/api/mobile/auth/refresh")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Refresh(
        [FromForm(Name = "refresh_token")] string refreshToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return BadRequest(new { error = "invalid_request", error_description = "refresh_token is required." });

        var discovery = await oidcBffService.FetchDiscoveryAsync(ct);
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();

        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _mobileAuthOptions.ClientId,
            ["client_secret"] = _mobileAuthOptions.ClientSecret,
            ["refresh_token"] = refreshToken,
        });

        var response = await client.PostAsync(discovery.TokenEndpoint, request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        return Content(payload, "application/json", Encoding.UTF8);
    }

    [HttpGet("/api/mobile/me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public IActionResult Me()
    {
        var claims = User.Claims.ToDictionary(c => c.Type, c => c.Value);
        return Ok(new
        {
            message = "Mobile bearer token validated by Demo.Bff.",
            claims,
        });
    }

}
