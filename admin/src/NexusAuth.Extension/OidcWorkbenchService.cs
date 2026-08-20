using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NexusAuth.Extension;

public class OidcWorkbenchService(
    IHttpClientFactory httpClientFactory,
    IFlowStateStore flowStateStore,
    IOptions<WorkbenchAuthOptions> authOptions) : IOidcWorkbenchService
{
    private readonly FrontendOptions _frontendOptions = new();
    private readonly WorkbenchAuthOptions _options = authOptions.Value;

    public string FrontendBaseUrl => _frontendOptions.BaseUrl.TrimEnd('/');

    public string Authority => _options.Authority;

    public string ClientId => _options.ClientId;

    public string? ClientSecret => _options.ClientSecret;

    public string RedirectUri => _options.RedirectUri;

    public string PostLogoutRedirectUri => _options.PostLogoutRedirectUri;

    public string Scope => _options.Scope;

    public bool SignOutProvider => _options.SignOutProvider;

    public IFlowStateStore FlowStateStore { get; } = flowStateStore;

    public async Task<DiscoveryDocument> FetchDiscoveryAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient();
        var response = await client.GetAsync($"{Authority.TrimEnd('/')}/.well-known/openid-configuration", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiscoveryDocument>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Unable to load OpenID Connect discovery document.");
    }

    public string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public (string codeChallenge, string codeVerifier) GeneratePkce()
    {
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        return (codeChallenge, codeVerifier);
    }

    public async Task<(string accessToken, string idToken, int expiresIn)> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct)
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

        var response = await client.PostAsync(discovery.TokenEndpoint, new FormUrlEncodedContent(form), ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Token exchange failed: {json}");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Missing access_token");
        var idToken = root.GetProperty("id_token").GetString()
            ?? throw new InvalidOperationException("Missing id_token");
        var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

        return (accessToken, idToken, expiresIn);
    }

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

public class FrontendOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5273";
}

public record PkcePair(string CodeVerifier, string CodeChallenge);