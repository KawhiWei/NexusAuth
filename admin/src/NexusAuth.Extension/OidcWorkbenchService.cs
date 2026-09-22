using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace NexusAuth.Extension;

/// <summary>
/// 中文：实现 Workbench 所需的 OpenID Connect 授权码、PKCE、刷新令牌和令牌内省操作。
/// English: Implements the OpenID Connect authorization-code, PKCE, token-refresh, and token-introspection operations used by Workbench.
/// </summary>
/// <param name="httpClientFactory">中文：用于创建访问 Provider 的 HTTP 客户端工厂。English: The factory used to create HTTP clients for Provider requests.</param>
/// <param name="flowStateStore">中文：用于保存授权流程临时安全状态的存储服务。English: The store used to retain temporary authorization-flow security state.</param>
/// <param name="authOptions">中文：Workbench OIDC 客户端配置。English: The Workbench OIDC client configuration.</param>
public class OidcWorkbenchService(
    IHttpClientFactory httpClientFactory,
    IFlowStateStore flowStateStore,
    IOptions<WorkbenchAuthOptions> authOptions) : IOidcWorkbenchService
{
    private readonly FrontendOptions _frontendOptions = new();
    private readonly WorkbenchAuthOptions _options = authOptions.Value;

    /// <summary>
    /// 中文：获取移除末尾斜杠后的前端基础地址。
    /// English: Gets the frontend base URL with its trailing slash removed.
    /// </summary>
    public string FrontendBaseUrl => _frontendOptions.BaseUrl.TrimEnd('/');

    /// <inheritdoc />
    public string Authority => _options.Authority;

    /// <inheritdoc />
    public string ClientId => _options.ClientId;

    /// <summary>
    /// 中文：获取配置的机密客户端凭据。
    /// English: Gets the configured confidential client credential.
    /// </summary>
    public string? ClientSecret => _options.ClientSecret;

    /// <inheritdoc />
    public string RedirectUri => _options.RedirectUri;

    /// <inheritdoc />
    public string PostLogoutRedirectUri => _options.PostLogoutRedirectUri;

    /// <inheritdoc />
    public string Scope => _options.Scope;

    /// <inheritdoc />
    public bool SignOutProvider => _options.SignOutProvider;

    /// <inheritdoc />
    public IFlowStateStore FlowStateStore { get; } = flowStateStore;

    /// <inheritdoc />
    public async Task<DiscoveryDocument> FetchDiscoveryAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient();
        var response = await client.GetAsync($"{Authority.TrimEnd('/')}/.well-known/openid-configuration", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiscoveryDocument>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Unable to load OpenID Connect discovery document.");
    }

    public async Task<ValidatedIdToken> ValidateIdTokenAsync(
        DiscoveryDocument discovery,
        string idToken,
        string expectedNonce,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentException.ThrowIfNullOrWhiteSpace(idToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedNonce);

        if (string.IsNullOrWhiteSpace(discovery.Issuer)
            || string.IsNullOrWhiteSpace(discovery.JwksUri))
            throw new SecurityTokenValidationException("Provider discovery metadata is incomplete.");

        var expectedIssuer = Authority.TrimEnd('/');
        if (!string.Equals(discovery.Issuer.TrimEnd('/'), expectedIssuer, StringComparison.Ordinal))
            throw new SecurityTokenInvalidIssuerException("Provider issuer does not match the configured authority.");

        var client = httpClientFactory.CreateClient();
        var jwksResponse = await client.GetAsync(
            new Uri(discovery.JwksUri, UriKind.Absolute),
            ct);
        jwksResponse.EnsureSuccessStatusCode();
        var jwks = new JsonWebKeySet(await jwksResponse.Content.ReadAsStringAsync(ct));

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(
            idToken,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = expectedIssuer,
                ValidateAudience = true,
                ValidAudience = ClientId,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = jwks.GetSigningKeys(),
                RequireSignedTokens = true,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromSeconds(30),
            },
            out var validatedToken);

        if (validatedToken is not JwtSecurityToken jwt
            || !string.Equals(jwt.Header.Alg, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal))
            throw new SecurityTokenValidationException("The ID token uses an unsupported signing algorithm.");

        var nonce = principal.FindFirst("nonce")?.Value;
        if (!FixedTimeEquals(nonce, expectedNonce))
            throw new SecurityTokenValidationException("OIDC nonce validation failed.");

        var subject = principal.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject))
            throw new SecurityTokenValidationException("The ID token does not contain a subject.");

        return new ValidatedIdToken(subject, principal.FindFirst("name")?.Value, principal.FindFirst("preferred_username")?.Value);
    }

    private static bool FixedTimeEquals(string? actual, string expected)
    {
        if (actual is null)
            return false;
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return actualBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    /// <inheritdoc />
    public string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// 中文：根据 PKCE code_verifier 计算 S256 code_challenge。
    /// English: Computes an S256 PKCE code_challenge from a code_verifier.
    /// </summary>
    /// <param name="codeVerifier">中文：要进行 SHA-256 摘要计算的 PKCE 验证码。English: The PKCE verifier to hash with SHA-256.</param>
    /// <returns>中文：Base64 URL 编码的 S256 挑战值。English: The Base64 URL-encoded S256 challenge.</returns>
    public string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <inheritdoc />
    public (string codeChallenge, string codeVerifier) GeneratePkce()
    {
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        return (codeChallenge, codeVerifier);
    }

    /// <inheritdoc />
    public async Task<WorkbenchTokenResult> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct)
    {
        var discovery = await FetchDiscoveryAsync(ct);

        var client = httpClientFactory.CreateClient();
        await ApplyClientAuthenticationAsync(client);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = codeVerifier,
        };

        var tokenEndpoint = new Uri(discovery.TokenEndpoint, UriKind.Absolute);
        var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form), ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Token exchange failed.");

        return ParseTokenResponse(json, requireIdToken: true);
    }

    /// <inheritdoc />
    public async Task<WorkbenchTokenResult> RefreshTokensAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("Refresh token is required.", nameof(refreshToken));

        var discovery = await FetchDiscoveryAsync(ct);

        var client = httpClientFactory.CreateClient();
        await ApplyClientAuthenticationAsync(client);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        };

        var tokenEndpoint = new Uri(discovery.TokenEndpoint, UriKind.Absolute);
        var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form), ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Refresh token request failed.");

        return ParseTokenResponse(json, requireIdToken: false);
    }

    /// <inheritdoc />
    public async Task<AccessTokenIntrospectionResult> IntrospectAccessTokenAsync(string accessToken, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var discovery = await FetchDiscoveryAsync(ct);
        if (string.IsNullOrWhiteSpace(discovery.IntrospectionEndpoint))
            throw new InvalidOperationException("The Provider discovery document does not contain an introspection endpoint.");

        var client = httpClientFactory.CreateClient();
        await ApplyClientAuthenticationAsync(client);
        var response = await client.PostAsync(
            new Uri(discovery.IntrospectionEndpoint, UriKind.Absolute),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = accessToken }),
            ct);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var payload = document.RootElement;
        var active = payload.TryGetProperty("active", out var activeProperty) && activeProperty.ValueKind == JsonValueKind.True;
        var subject = payload.TryGetProperty("sub", out var subjectProperty) && subjectProperty.ValueKind == JsonValueKind.String
            ? subjectProperty.GetString()
            : null;
        var clientId = payload.TryGetProperty("client_id", out var clientIdProperty) && clientIdProperty.ValueKind == JsonValueKind.String
            ? clientIdProperty.GetString()
            : null;

        return new AccessTokenIntrospectionResult(active, subject, clientId);
    }

    /// <summary>
    /// 中文：解析并校验令牌端点返回的 JSON 载荷。
    /// English: Parses and validates the JSON payload returned by the token endpoint.
    /// </summary>
    /// <param name="json">中文：令牌端点返回的 JSON 文本。English: The JSON text returned by the token endpoint.</param>
    /// <param name="requireIdToken">中文：是否要求响应必须包含 ID Token。English: Whether the response must contain an ID token.</param>
    /// <returns>中文：经过校验的令牌结果。English: The validated token result.</returns>
    private static WorkbenchTokenResult ParseTokenResponse(string json, bool requireIdToken)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Missing access_token");
        var refreshToken = root.TryGetProperty("refresh_token", out var refreshTokenElement)
            ? refreshTokenElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("Missing refresh_token");

        var idToken = root.TryGetProperty("id_token", out var idTokenElement)
            ? idTokenElement.GetString()
            : null;
        if (requireIdToken && string.IsNullOrWhiteSpace(idToken))
            throw new InvalidOperationException("Missing id_token");

        var expiresIn = root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var parsedExpiresIn)
            ? parsedExpiresIn
            : 3600;
        if (expiresIn <= 0)
            throw new InvalidOperationException("Invalid expires_in");

        return new WorkbenchTokenResult(accessToken, refreshToken, idToken, expiresIn);
    }

    /// <summary>
    /// 中文：使用 HTTP Basic 方式把当前客户端凭据添加到 HTTP 客户端。
    /// English: Adds the current client credentials to an HTTP client using HTTP Basic authentication.
    /// </summary>
    /// <param name="client">中文：要配置认证请求头的 HTTP 客户端。English: The HTTP client whose authentication header is configured.</param>
    /// <returns>中文：表示配置完成的任务。English: A task representing completion of the configuration.</returns>
    /// <exception cref="InvalidOperationException">中文：未配置客户端密钥。English: The client secret has not been configured.</exception>
    public Task ApplyClientAuthenticationAsync(HttpClient client)
    {
        var actualClientId = ClientId;
        var actualClientSecret = ClientSecret;
        if (string.IsNullOrWhiteSpace(actualClientSecret))
            throw new InvalidOperationException("ClientSecret is required.");

        var credentialBytes = Encoding.UTF8.GetBytes($"{actualClientId}:{actualClientSecret}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentialBytes));
        return Task.CompletedTask;
    }
}

/// <summary>
/// 中文：配置 Workbench 前端应用的访问地址。
/// English: Configures the URL used to access the Workbench frontend application.
/// </summary>
public class FrontendOptions
{
    /// <summary>
    /// 中文：获取或设置 Workbench 前端基础地址。
    /// English: Gets or sets the Workbench frontend base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5273";
}

/// <summary>
/// 中文：表示一组 PKCE 验证码和挑战值。
/// English: Represents a PKCE verifier and challenge pair.
/// </summary>
/// <param name="CodeVerifier">中文：随机生成的原始 PKCE 验证码。English: The original randomly generated PKCE verifier.</param>
/// <param name="CodeChallenge">中文：由验证码计算得到的 S256 挑战值。English: The S256 challenge derived from the verifier.</param>
public record PkcePair(string CodeVerifier, string CodeChallenge);
