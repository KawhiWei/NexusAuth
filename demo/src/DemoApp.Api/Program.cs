using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────
// Configuration
// ──────────────────────────────────────────────

var nexusAuth = builder.Configuration.GetSection("NexusAuth");
var authority = nexusAuth["Authority"]!;       // NexusAuth base URL
var clientId = nexusAuth["ClientId"]!;
var clientSecret = nexusAuth["ClientSecret"]!;
var redirectUri = nexusAuth["RedirectUri"]!;
var scope = nexusAuth["Scope"]!;

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]!;
var issuer = jwtSection["Issuer"]!;
var audience = jwtSection["Audience"]!;

// ──────────────────────────────────────────────
// Services
// ──────────────────────────────────────────────

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpClient();

var app = builder.Build();

// ──────────────────────────────────────────────
// Middleware
// ──────────────────────────────────────────────

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// ──────────────────────────────────────────────
// PKCE helper: stored in-memory (per-session via state parameter)
// ──────────────────────────────────────────────

// state → code_verifier mapping (in production, use a proper session store)
var pkceStore = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();

// ──────────────────────────────────────────────
// API: GET /api/config — return OAuth config for frontend
// ──────────────────────────────────────────────

app.MapGet("/api/config", () => Results.Ok(new
{
    authority,
    clientId,
    redirectUri,
    scope,
}));

// ──────────────────────────────────────────────
// API: GET /api/login — start OAuth2 PKCE flow
// ──────────────────────────────────────────────

app.MapGet("/api/login", () =>
{
    // Generate PKCE code_verifier and code_challenge
    var codeVerifier = GenerateCodeVerifier();
    var codeChallenge = GenerateCodeChallenge(codeVerifier);
    var state = Guid.NewGuid().ToString("N");

    // Store code_verifier keyed by state
    pkceStore[state] = codeVerifier;

    // Build NexusAuth authorize URL
    var authorizeUrl = $"{authority}/connect/authorize" +
                       $"?response_type=code" +
                       $"&client_id={Uri.EscapeDataString(clientId)}" +
                       $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                       $"&scope={Uri.EscapeDataString(scope)}" +
                       $"&state={Uri.EscapeDataString(state)}" +
                       $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
                       $"&code_challenge_method=S256";

    return Results.Ok(new { authorizeUrl, state });
});

// ──────────────────────────────────────────────
// Page: GET /callback — OAuth2 callback (renders HTML that extracts code)
// ──────────────────────────────────────────────

app.MapGet("/callback", () =>
{
    // Return a small HTML page that reads ?code=...&state=... from URL
    // and calls /api/token to exchange code for tokens
    var html = """
    <!DOCTYPE html>
    <html><head><title>Processing...</title></head>
    <body>
    <p>Processing login, please wait...</p>
    <script>
        (async () => {
            const params = new URLSearchParams(window.location.search);
            const code = params.get('code');
            const state = params.get('state');
            if (!code || !state) {
                document.body.innerHTML = '<p style="color:red">Missing code or state parameter.</p>';
                return;
            }
            try {
                const resp = await fetch('/api/token', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ code, state })
                });
                const data = await resp.json();
                if (data.access_token) {
                    localStorage.setItem('access_token', data.access_token);
                    if (data.refresh_token) localStorage.setItem('refresh_token', data.refresh_token);
                    window.location.href = '/';
                } else {
                    document.body.innerHTML = '<p style="color:red">Token exchange failed: ' + JSON.stringify(data) + '</p>';
                }
            } catch (e) {
                document.body.innerHTML = '<p style="color:red">Error: ' + e.message + '</p>';
            }
        })();
    </script>
    </body></html>
    """;
    return Results.Content(html, "text/html");
});

// ──────────────────────────────────────────────
// API: POST /api/token — exchange authorization code for tokens
// ──────────────────────────────────────────────

app.MapPost("/api/token", async (HttpContext ctx, IHttpClientFactory httpClientFactory) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
    var code = body.GetProperty("code").GetString();
    var state = body.GetProperty("state").GetString();

    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        return Results.BadRequest(new { error = "code and state are required" });

    // Retrieve code_verifier from PKCE store
    if (!pkceStore.TryRemove(state, out var codeVerifier))
        return Results.BadRequest(new { error = "Invalid or expired state. Please try logging in again." });

    // Exchange code for tokens at NexusAuth /connect/token
    var client = httpClientFactory.CreateClient();
    var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["grant_type"] = "authorization_code",
        ["code"] = code,
        ["redirect_uri"] = redirectUri,
        ["client_id"] = clientId,
        ["client_secret"] = clientSecret,
        ["code_verifier"] = codeVerifier,
    });

    var tokenResponse = await client.PostAsync($"{authority}/connect/token", tokenRequest);
    var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

    if (!tokenResponse.IsSuccessStatusCode)
        return Results.Json(JsonSerializer.Deserialize<JsonElement>(tokenJson), statusCode: (int)tokenResponse.StatusCode);

    return Results.Content(tokenJson, "application/json");
});

// ──────────────────────────────────────────────
// API: POST /api/refresh — refresh access token
// ──────────────────────────────────────────────

app.MapPost("/api/refresh", async (HttpContext ctx, IHttpClientFactory httpClientFactory) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
    var refreshToken = body.GetProperty("refresh_token").GetString();

    if (string.IsNullOrWhiteSpace(refreshToken))
        return Results.BadRequest(new { error = "refresh_token is required" });

    var client = httpClientFactory.CreateClient();
    var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["grant_type"] = "refresh_token",
        ["refresh_token"] = refreshToken,
        ["client_id"] = clientId,
        ["client_secret"] = clientSecret,
    });

    var tokenResponse = await client.PostAsync($"{authority}/connect/token", tokenRequest);
    var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

    if (!tokenResponse.IsSuccessStatusCode)
        return Results.Json(JsonSerializer.Deserialize<JsonElement>(tokenJson), statusCode: (int)tokenResponse.StatusCode);

    return Results.Content(tokenJson, "application/json");
});

// ──────────────────────────────────────────────
// API: GET /api/me — protected endpoint (requires JWT)
// ──────────────────────────────────────────────

app.MapGet("/api/me", (HttpContext ctx) =>
{
    var claims = ctx.User.Claims.ToDictionary(c => c.Type, c => c.Value);
    return Results.Ok(new
    {
        message = "You are authenticated!",
        claims,
    });
}).RequireAuthorization();

// ──────────────────────────────────────────────
// Fallback: serve index.html for SPA routing
// ──────────────────────────────────────────────

app.MapFallbackToFile("index.html");

app.Run();

// ──────────────────────────────────────────────
// PKCE Helpers
// ──────────────────────────────────────────────

static string GenerateCodeVerifier()
{
    var bytes = new byte[32];
    RandomNumberGenerator.Fill(bytes);
    return Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}

static string GenerateCodeChallenge(string codeVerifier)
{
    var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
    return Convert.ToBase64String(hash)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}
