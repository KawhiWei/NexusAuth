using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NexusAuth.Application.Services;

namespace NexusAuth.Host.Controllers;

[ApiController]
public class OpenIdController : ControllerBase
{
    private readonly ITokenSigningCredentialsProvider _signingCredentialsProvider;
    private readonly ITokenService _tokenService;
    private readonly IUserService _userService;
    private readonly IClientService _clientService;
    private readonly IDeviceAuthorizationService _deviceAuthorizationService;
    private readonly JwtOptions _jwtOptions;
    private static readonly HashSet<string> BaseUserInfoClaims =
    [
        "sub",
        "preferred_username",
        "name",
        "email",
        "phone_number",
        "email_verified",
        "phone_number_verified",
    ];

    public OpenIdController(
        ITokenSigningCredentialsProvider signingCredentialsProvider,
        ITokenService tokenService,
        IUserService userService,
        IClientService clientService,
        IDeviceAuthorizationService deviceAuthorizationService,
        IOptions<JwtOptions> jwtOptions)
    {
        _signingCredentialsProvider = signingCredentialsProvider;
        _tokenService = tokenService;
        _userService = userService;
        _clientService = clientService;
        _deviceAuthorizationService = deviceAuthorizationService;
        _jwtOptions = jwtOptions.Value;
    }

    [HttpGet("/.well-known/openid-configuration")]
    public IActionResult Discovery()
    {
        var issuer = GetIssuer();
        return Ok(new
        {
            issuer,
            authorization_endpoint = $"{issuer}/connect/authorize",
            token_endpoint = $"{issuer}/connect/token",
            userinfo_endpoint = $"{issuer}/connect/userinfo",
            jwks_uri = $"{issuer}/.well-known/jwks.json",
            revocation_endpoint = $"{issuer}/connect/revocation",
            introspection_endpoint = $"{issuer}/connect/introspect",
            device_authorization_endpoint = $"{issuer}/connect/deviceauthorization",
            end_session_endpoint = $"{issuer}/connect/endsession",
            scopes_supported = new[] { "openid", "profile", "email", "phone", "offline_access" },
            response_types_supported = new[] { "code" },
            grant_types_supported = new[]
            {
                "authorization_code",
                "client_credentials",
                "refresh_token",
                "urn:ietf:params:oauth:grant-type:device_code",
            },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" },
            token_endpoint_auth_methods_supported = new[] { "client_secret_post" },
            claims_supported = new[]
            {
                "sub", "preferred_username", "name", "email", "phone_number", "email_verified",
                "phone_number_verified", "nonce", "at_hash", "auth_time", "acr", "amr"
            },
            code_challenge_methods_supported = new[] { "plain", "S256" },
            claims_parameter_supported = true,
            request_parameter_supported = false,
            request_uri_parameter_supported = false,
            prompt_values_supported = new[] { "none", "login" },
        });
    }

    [HttpGet("/.well-known/jwks.json")]
    public IActionResult Jwks()
    {
        return Ok(new { keys = new[] { _signingCredentialsProvider.GetJwk() } });
    }

    [HttpGet("/connect/userinfo")]
    public async Task<IActionResult> UserInfo(CancellationToken ct)
    {
        var authorization = Request.Headers.Authorization.ToString();
        var bearerToken = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..]
            : null;

        if (string.IsNullOrWhiteSpace(bearerToken))
            return Unauthorized(new { error = "invalid_token", error_description = "Bearer token is required." });

        var introspection = await _tokenService.IntrospectAsync(bearerToken, ct);
        if (!introspection.Active)
            return Unauthorized(new { error = "invalid_token", error_description = "Access token is invalid." });

        if (!Guid.TryParse(introspection.Subject, out var userId))
            return Unauthorized(new { error = "invalid_token", error_description = "Access token does not represent a user." });

        var user = await _userService.FindByIdAsync(userId, ct);
        if (user is null)
            return Unauthorized(new { error = "invalid_token", error_description = "User not found." });

        var requestedClaims = ResolveRequestedClaims(introspection.Scope);

        return Ok(BuildUserInfoPayload(user, requestedClaims));
    }

    [HttpPost("/connect/deviceauthorization")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> DeviceAuthorization(
        [FromForm(Name = "client_id")] string? clientId,
        [FromForm(Name = "client_secret")] string? clientSecret,
        [FromForm] string? scope,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return BadRequest(new { error = "invalid_request", error_description = "client_id is required." });

        if (string.IsNullOrWhiteSpace(scope))
            return BadRequest(new { error = "invalid_request", error_description = "scope is required." });

        var result = await _deviceAuthorizationService.StartAsync(clientId, clientSecret, scope, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorCode, error_description = result.Error });

        var issuer = GetIssuer();
        var verificationUri = $"{issuer}{result.VerificationUri}";
        var verificationUriComplete = $"{issuer}{result.VerificationUriComplete}";

        return Ok(new
        {
            device_code = result.DeviceCode,
            user_code = result.UserCode,
            verification_uri = verificationUri,
            verification_uri_complete = verificationUriComplete,
            expires_in = result.ExpiresIn,
            interval = result.Interval,
        });
    }

    [HttpPost("/connect/introspect")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Introspect(
        [FromForm(Name = "client_id")] string? clientId,
        [FromForm(Name = "client_secret")] string? clientSecret,
        [FromForm] string? token,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return Unauthorized(new { error = "invalid_client" });

        var authentication = await _clientService.AuthenticateClientAsync(clientId, clientSecret, requireSecret: true, ct);
        if (!authentication.IsSuccess)
            return Unauthorized(new { error = "invalid_client" });

        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "invalid_request", error_description = "token is required." });

        var result = await _tokenService.IntrospectAsync(token, ct);
        return Ok(new
        {
            active = result.Active,
            sub = result.Subject,
            client_id = result.ClientId,
            scope = result.Scope,
            exp = result.Exp,
            iss = result.Issuer,
            token_use = result.TokenUse,
        });
    }

    [HttpPost("/connect/revocation")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Revoke(
        [FromForm(Name = "client_id")] string? clientId,
        [FromForm(Name = "client_secret")] string? clientSecret,
        [FromForm] string? token,
        [FromForm(Name = "token_type_hint")] string? tokenTypeHint,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return Unauthorized(new { error = "invalid_client" });

        var authentication = await _clientService.AuthenticateClientAsync(clientId, clientSecret, requireSecret: true, ct);
        if (!authentication.IsSuccess)
            return Unauthorized(new { error = "invalid_client" });

        if (!string.IsNullOrWhiteSpace(token))
        {
            if (string.Equals(tokenTypeHint, "refresh_token", StringComparison.OrdinalIgnoreCase))
                await _tokenService.RevokeRefreshTokenAsync(token, ct);
            else
            {
                await _tokenService.RevokeAccessTokenAsync(token, ct);
                await _tokenService.RevokeRefreshTokenAsync(token, ct);
            }
        }

        return Ok();
    }

    [HttpGet("/connect/endsession")]
    public async Task<IActionResult> EndSession([FromQuery(Name = "post_logout_redirect_uri")] string? postLogoutRedirectUri = null)
    {
        await HttpContext.SignOutAsync(AppWebModule.AuthenticationScheme);

        if (!string.IsNullOrWhiteSpace(postLogoutRedirectUri) && Uri.TryCreate(postLogoutRedirectUri, UriKind.Absolute, out _))
            return Redirect(postLogoutRedirectUri);

        return Redirect("/");
    }

    private string GetIssuer()
    {
        // 统一使用配置中的 Issuer，确保 discovery 的 issuer 与 token 的 iss 完全一致。
        return _jwtOptions.Issuer.TrimEnd('/');
    }

    private static OidcRequestedClaims ResolveRequestedClaims(string? scope)
    {
        // 中文注释：当前 access token 里只保留了 scope，没有携带原始 claims 参数。
        // 因此 userinfo 先按 scope 映射基础 claim，并允许未来继续扩展为更精细的 claims 透传。
        var userInfoClaims = new HashSet<string>(BaseUserInfoClaims, StringComparer.Ordinal);
        var requestedScopes = scope?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal)
            ?? [];

        if (!requestedScopes.Contains("email"))
        {
            userInfoClaims.Remove("email");
            userInfoClaims.Remove("email_verified");
        }

        if (!requestedScopes.Contains("phone"))
        {
            userInfoClaims.Remove("phone_number");
            userInfoClaims.Remove("phone_number_verified");
        }

        return OidcRequestedClaims.Create(userInfoClaims: userInfoClaims);
    }

    private static object BuildUserInfoPayload(Domain.AggregateRoots.Users.User user, OidcRequestedClaims requestedClaims)
    {
        var payload = new Dictionary<string, object?>
        {
            ["sub"] = user.Id.ToString(),
            ["preferred_username"] = user.Username,
            ["name"] = user.Nickname,
        };

        if (requestedClaims.RequestsUserInfoClaim("email"))
            payload["email"] = user.Email;

        if (requestedClaims.RequestsUserInfoClaim("email_verified"))
            payload["email_verified"] = false;

        if (requestedClaims.RequestsUserInfoClaim("phone_number"))
            payload["phone_number"] = user.PhoneNumber;

        if (requestedClaims.RequestsUserInfoClaim("phone_number_verified"))
            payload["phone_number_verified"] = false;

        return payload;
    }
}
