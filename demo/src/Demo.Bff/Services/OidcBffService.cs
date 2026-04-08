using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Demo.Bff.Models;
using Demo.Bff.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace Demo.Bff.Services;

public class OidcBffService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FrontendOptions _frontendOptions;
    private readonly NexusAuthBffOptions _authOptions;

    public OidcBffService(
        IHttpClientFactory httpClientFactory,
        IOptions<FrontendOptions> frontendOptions,
        IOptions<NexusAuthBffOptions> authOptions)
    {
        _httpClientFactory = httpClientFactory;
        _frontendOptions = frontendOptions.Value;
        _authOptions = authOptions.Value;
    }

    public string FrontendBaseUrl => _frontendOptions.BaseUrl.TrimEnd('/');

    public string Authority => _authOptions.Authority;

    public string ClientId => _authOptions.ClientId;

    public string ClientSecret => _authOptions.ClientSecret;

    public string RedirectUri => _authOptions.RedirectUri;

    public string PostLogoutRedirectUri => _authOptions.PostLogoutRedirectUri;

    public string Scope => _authOptions.Scope;

    public async Task<DiscoveryDocument> FetchDiscoveryAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync($"{Authority.TrimEnd('/')}/.well-known/openid-configuration", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiscoveryDocument>(cancellationToken: ct)
               ?? throw new InvalidOperationException("Unable to load OpenID Connect discovery document.");
    }

    public string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncoder.Encode(bytes);
    }

    public string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncoder.Encode(hash);
    }

    public PkcePair GeneratePkce()
    {
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        return new PkcePair(codeVerifier, codeChallenge);
    }

    public SessionPayload? ReadSession(ClaimsPrincipal principal)
    {
        var payload = principal.FindFirst("session_payload")?.Value;
        return string.IsNullOrWhiteSpace(payload) ? null : JsonSerializer.Deserialize<SessionPayload>(payload);
    }

    public bool IsExpired(SessionPayload session)
    {
        return session.IssuedAtUtc.AddSeconds(session.ExpiresIn - 30) <= DateTimeOffset.UtcNow;
    }

    public async Task<ValidatedIdToken> ValidateIdTokenAsync(DiscoveryDocument discovery, string idToken, string expectedNonce, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var jwksResponse = await client.GetAsync(discovery.JwksUri, ct);
        jwksResponse.EnsureSuccessStatusCode();

        var jwks = new JsonWebKeySet(await jwksResponse.Content.ReadAsStringAsync(ct));
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(idToken, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = discovery.Issuer,
            ValidateAudience = true,
            ValidAudience = ClientId,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        }, out _);

        var nonce = principal.FindFirst("nonce")?.Value;
        if (!string.Equals(nonce, expectedNonce, StringComparison.Ordinal))
            throw new SecurityTokenValidationException("OIDC nonce validation failed.");

        return new ValidatedIdToken(
            principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            principal.FindFirst("name")?.Value,
            principal.FindFirst("preferred_username")?.Value);
    }

    public async Task<DemoUserInfo> FetchUserInfoAsync(string userInfoEndpoint, string accessToken, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, userInfoEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DemoUserInfo>(cancellationToken: ct)
               ?? throw new InvalidOperationException("Unable to parse userinfo response.");
    }

    public async Task RevokeIfPresentAsync(string revocationEndpoint, string? token, string tokenTypeHint, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        var client = _httpClientFactory.CreateClient();
        ApplyClientAuthentication(client);
        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = token,
            ["token_type_hint"] = tokenTypeHint,
        });

        await client.PostAsync(revocationEndpoint, request, ct);
    }

    /// <summary>
    /// 给下游 Token/Revocation 请求附加 client_secret_basic 认证头。
    /// 主要调用方：Web BFF 调 NexusAuth 的后端交换 token 场景。
    /// </summary>
    public void ApplyClientAuthentication(HttpClient client, string? clientId = null, string? clientSecret = null)
    {
        var actualClientId = clientId ?? ClientId;
        var actualClientSecret = clientSecret ?? ClientSecret;
        var credentialBytes = Encoding.UTF8.GetBytes($"{actualClientId}:{actualClientSecret}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentialBytes));
    }
}

public record PkcePair(string CodeVerifier, string CodeChallenge);
