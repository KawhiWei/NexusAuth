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

    /// <summary>
    /// OIDC Discovery 元数据端点。
    /// </summary>
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
            scopes_supported = new[] { "openid", "profile", "email", "phone", "address", "offline_access" },
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
            token_endpoint_auth_methods_supported = new[] { "client_secret_post", "client_secret_basic" },
            claims_supported = new[]
            {
                "sub", "preferred_username", "name", "nickname", "email", "phone_number", "email_verified",
                "phone_number_verified", "gender", "ethnicity", "address", "nonce", "at_hash", "auth_time", "acr", "amr"
            },
            code_challenge_methods_supported = new[] { "S256" },
            claims_parameter_supported = true,
            request_parameter_supported = false,
            request_uri_parameter_supported = false,
            prompt_values_supported = new[] { "none", "login", "consent" },
        });
    }

    /// <summary>
    /// JWKS 公钥端点。
    /// </summary>
    [HttpGet("/.well-known/jwks.json")]
    public IActionResult Jwks()
    {
        return Ok(new { keys = new[] { _signingCredentialsProvider.GetJwk() } });
    }

    /// <summary>
    /// OIDC UserInfo 端点。
    /// </summary>
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

        if (!string.Equals(introspection.TokenUse, "access_token", StringComparison.Ordinal))
            return Unauthorized(new { error = "invalid_token", error_description = "Only access tokens can call userinfo." });

        if (!Guid.TryParse(introspection.Subject, out var userId))
            return Unauthorized(new { error = "invalid_token", error_description = "Access token does not represent a user." });

        var user = await _userService.FindByIdAsync(userId, ct);
        if (user is null)
            return Unauthorized(new { error = "invalid_token", error_description = "User not found." });

        var requestedClaims = ResolveRequestedClaims(introspection.Scope, introspection.ClaimsJson);

        return Ok(BuildUserInfoPayload(user, requestedClaims));
    }

    /// <summary>
    /// Device Authorization 端点。
    /// </summary>
    [HttpPost("/connect/deviceauthorization")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> DeviceAuthorization(
        [FromForm(Name = "client_id")] string? clientId,
        [FromForm(Name = "client_secret")] string? clientSecret,
        [FromForm] string? scope,
        CancellationToken ct = default)
    {
        var resolvedClientAuthentication = OAuthClientAuthenticationParser.ResolveClientAuthentication(
            Request.Headers.Authorization.ToString(),
            clientId,
            clientSecret);
        clientId = resolvedClientAuthentication.ClientId;
        clientSecret = resolvedClientAuthentication.ClientSecret;

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

    /// <summary>
    /// OAuth2 Introspection 端点。
    /// </summary>
    [HttpPost("/connect/introspect")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Introspect(
        [FromForm(Name = "client_id")] string? clientId,
        [FromForm(Name = "client_secret")] string? clientSecret,
        [FromForm] string? token,
        CancellationToken ct = default)
    {
        var resolvedClientAuthentication = OAuthClientAuthenticationParser.ResolveClientAuthentication(
            Request.Headers.Authorization.ToString(),
            clientId,
            clientSecret);
        clientId = resolvedClientAuthentication.ClientId;
        clientSecret = resolvedClientAuthentication.ClientSecret;

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return Unauthorized(new { error = "invalid_client" });

        var authentication = await _clientService.AuthenticateClientAsync(clientId, clientSecret, requireSecret: false, ct);
        if (!authentication.IsSuccess)
            return Unauthorized(new { error = "invalid_client" });

        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "invalid_request", error_description = "token is required." });

        var result = await _tokenService.IntrospectAsync(token, clientId, ct);
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

    /// <summary>
    /// OAuth2 Revocation 端点。
    /// </summary>
    [HttpPost("/connect/revocation")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Revoke(
        [FromForm(Name = "client_id")] string? clientId,
        [FromForm(Name = "client_secret")] string? clientSecret,
        [FromForm] string? token,
        [FromForm(Name = "token_type_hint")] string? tokenTypeHint,
        CancellationToken ct = default)
    {
        var resolvedClientAuthentication = OAuthClientAuthenticationParser.ResolveClientAuthentication(
            Request.Headers.Authorization.ToString(),
            clientId,
            clientSecret);
        clientId = resolvedClientAuthentication.ClientId;
        clientSecret = resolvedClientAuthentication.ClientSecret;

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return Unauthorized(new { error = "invalid_client" });

        var authentication = await _clientService.AuthenticateClientAsync(clientId, clientSecret, requireSecret: false, ct);
        if (!authentication.IsSuccess)
            return Unauthorized(new { error = "invalid_client" });

        if (!string.IsNullOrWhiteSpace(token))
        {
            if (string.Equals(tokenTypeHint, "refresh_token", StringComparison.OrdinalIgnoreCase))
                await RevokeRefreshTokenForClientAsync(token, clientId, ct);
            else
            {
                await _tokenService.RevokeAccessTokenAsync(token, clientId, ct);
                await RevokeRefreshTokenForClientAsync(token, clientId, ct);
            }
        }

        return Ok();
    }

    /// <summary>
    /// OIDC RP-Initiated Logout 端点。
    /// </summary>
    [HttpGet("/connect/endsession")]
    public async Task<IActionResult> EndSession(
        [FromQuery(Name = "id_token_hint")] string? idTokenHint = null,
        [FromQuery(Name = "post_logout_redirect_uri")] string? postLogoutRedirectUri = null,
        [FromQuery] string? state = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(idTokenHint))
        {
            var introspection = await _tokenService.IntrospectAsync(idTokenHint, ct);
            if (!introspection.Active || !string.Equals(introspection.TokenUse, "id_token", StringComparison.Ordinal))
                return BadRequest(new { error = "invalid_request", error_description = "id_token_hint is invalid." });

            if (string.IsNullOrWhiteSpace(introspection.ClientId))
                return BadRequest(new { error = "invalid_request", error_description = "id_token_hint does not identify a client." });

            var clientValidation = await _clientService.AuthenticateClientForPostLogoutAsync(introspection.ClientId, postLogoutRedirectUri, ct);
            if (!clientValidation.IsSuccess)
                return BadRequest(new { error = clientValidation.ErrorCode ?? "invalid_request", error_description = clientValidation.Error });
        }
        else if (!string.IsNullOrWhiteSpace(postLogoutRedirectUri))
        {
            return BadRequest(new { error = "invalid_request", error_description = "id_token_hint is required when post_logout_redirect_uri is provided." });
        }

        await HttpContext.SignOutAsync(AppWebModule.AuthenticationScheme);

        if (!string.IsNullOrWhiteSpace(postLogoutRedirectUri) && Uri.TryCreate(postLogoutRedirectUri, UriKind.Absolute, out _))
        {
            var redirectUri = postLogoutRedirectUri;
            if (!string.IsNullOrWhiteSpace(state))
                redirectUri = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(redirectUri, "state", state);

            return Redirect(redirectUri);
        }

        return Redirect("/");
    }

    private string GetIssuer()
    {
        // 统一使用配置中的 Issuer，确保 discovery 的 issuer 与 token 的 iss 完全一致。
        return _jwtOptions.Issuer.TrimEnd('/');
    }

    private static OidcRequestedClaims ResolveRequestedClaims(string? scope, string? claimsJson)
    {
        // 中文注释：优先使用授权阶段传入的 OIDC claims 参数，
        // 没有显式 claims 请求时，再按 scope 做基础 claim 回退。
        var requestedClaims = OidcRequestedClaims.Parse(claimsJson);
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

        if (requestedClaims.HasExplicitRequests)
        {
            userInfoClaims.UnionWith(requestedClaims.UserInfoClaims);
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

        if (requestedClaims.RequestsUserInfoClaim("nickname"))
            payload["nickname"] = user.Nickname;

        if (OidcClaimEmissionPolicy.ShouldEmitRequestedClaim(requestedClaims, "email", user.Email))
            payload["email"] = user.Email;

        if (requestedClaims.RequestsUserInfoClaim("email_verified"))
            payload["email_verified"] = false;

        if (OidcClaimEmissionPolicy.ShouldEmitRequestedClaim(requestedClaims, "phone_number", user.PhoneNumber))
            payload["phone_number"] = user.PhoneNumber;

        if (requestedClaims.RequestsUserInfoClaim("phone_number_verified"))
            payload["phone_number_verified"] = false;

        if (OidcClaimEmissionPolicy.ShouldEmitRequestedClaim(requestedClaims, "gender", user.Gender.ToString()))
            payload["gender"] = user.Gender.ToString();

        if (OidcClaimEmissionPolicy.ShouldEmitRequestedClaim(requestedClaims, "ethnicity", user.Ethnicity))
            payload["ethnicity"] = user.Ethnicity;

        if (requestedClaims.RequestsUserInfoClaim("address"))
        {
            payload["address"] = new Dictionary<string, object?>
            {
                ["formatted"] = null,
            };
        }

        return payload;
    }

    private async Task RevokeRefreshTokenForClientAsync(string token, string clientId, CancellationToken ct)
    {
        var isOwnedByClient = await _tokenService.IsRefreshTokenOwnedByClientAsync(token, clientId, ct);
        if (!isOwnedByClient)
            return;

        await _tokenService.RevokeRefreshTokenAsync(token, clientId, ct);
    }
}
