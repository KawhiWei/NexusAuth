using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using NexusAuth.Extension;
using Xunit;

namespace NexusAuth.Workbench.Api.IntegrationTests;

public sealed class WorkbenchGatewayModeTests
{
    private const string Authority = "https://issuer.example.test";
    private const string Audience = "nexusauth.workbench.api";

    [Fact]
    public async Task Gateway_registers_only_bearer_and_no_interactive_login_services()
    {
        using var factory = CreateFactory(true);
        var schemes = await factory.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();

        Assert.Equal(WorkbenchAuthenticationDefaults.BearerScheme, Assert.Single(schemes).Name);
        using var scope = factory.Services.CreateScope();
        Assert.Null(scope.ServiceProvider.GetService<IOidcWorkbenchService>());
        Assert.Null(scope.ServiceProvider.GetService<IFlowStateStore>());
        Assert.Null(scope.ServiceProvider.GetService<WorkbenchCookieAuthenticationEvents>());
    }

    [Fact]
    public async Task Direct_registers_cookie_bearer_and_interactive_login_services()
    {
        using var factory = CreateFactory(false);
        var schemes = (await factory.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync()).ToArray();

        Assert.Contains(schemes, scheme => scheme.Name == WorkbenchAuthenticationDefaults.CookieScheme);
        Assert.Contains(schemes, scheme => scheme.Name == WorkbenchAuthenticationDefaults.BearerScheme);
        Assert.Contains(schemes, scheme => scheme.Name == WorkbenchAuthenticationDefaults.Scheme);
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IOidcWorkbenchService>());
        Assert.NotNull(scope.ServiceProvider.GetService<IFlowStateStore>());
        Assert.NotNull(scope.ServiceProvider.GetService<WorkbenchCookieAuthenticationEvents>());
    }

    [Theory]
    [InlineData("GET", "/api/auth/login")]
    [InlineData("GET", "/api/auth/config")]
    [InlineData("GET", "/signin-oidc")]
    [InlineData("POST", "/api/auth/logout")]
    public async Task Gateway_does_not_publish_interactive_login_routes(string method, string path)
    {
        using var factory = CreateFactory(true);
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    public async Task Gateway_does_not_trust_a_request_without_a_valid_token(string? token)
    {
        using var factory = CreateFactory(true);
        using var client = factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Theory]
    [InlineData("access_token", Audience, Authority, 200)]
    [InlineData("id_token", Audience, Authority, 401)]
    [InlineData("access_token", "different-api", Authority, 401)]
    [InlineData("access_token", Audience, "https://other.example.test", 401)]
    public async Task Gateway_validates_signature_token_use_audience_and_issuer(
        string tokenUse, string audience, string issuer, int expectedStatus)
    {
        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = "test-key" };
        using var factory = CreateFactory(true, signingKey: signingKey);
        using var client = factory.CreateClient();
        var token = new JwtSecurityToken(
            issuer, audience,
            [new Claim("sub", "gateway-user"), new Claim("name", "Gateway User"), new Claim("token_use", tokenUse)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));

        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        if (expectedStatus == 200)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Gateway User", body.RootElement.GetProperty("user").GetProperty("name").GetString());
            Assert.Equal("gateway-user", body.RootElement.GetProperty("user").GetProperty("id").GetString());
        }
    }

    [Theory]
    [InlineData("missing-token-use")]
    [InlineData("expired")]
    [InlineData("wrong-signature")]
    public async Task Gateway_rejects_missing_token_use_expired_tokens_and_invalid_signatures(string scenario)
    {
        using var rsa = RSA.Create(2048);
        using var otherRsa = RSA.Create(2048);
        var trustedKey = new RsaSecurityKey(rsa) { KeyId = "test-key" };
        var untrustedKey = new RsaSecurityKey(otherRsa) { KeyId = "test-key" };
        using var factory = CreateFactory(true, signingKey: trustedKey);
        using var client = factory.CreateClient();
        var claims = new List<Claim> { new("sub", "gateway-user"), new("name", "Gateway User") };
        if (scenario != "missing-token-use")
            claims.Add(new Claim("token_use", "access_token"));
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            Authority, Audience, claims,
            now.AddHours(-2), scenario == "expired" ? now.AddHours(-1) : now.AddMinutes(5),
            new SigningCredentials(scenario == "wrong-signature" ? untrustedKey : trustedKey, SecurityAlgorithms.RsaSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));

        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Gateway_allows_empty_interactive_login_configuration()
    {
        using var factory = CreateFactory(true, builder => builder
            .UseSetting("Auth:RedirectUri", "")
            .UseSetting("Auth:PostLogoutRedirectUri", "")
            .UseSetting("Auth:Scope", ""));

        Assert.NotNull(factory.Services.GetRequiredService<IAuthenticationSchemeProvider>());
    }

    [Theory]
    [InlineData("Auth:Authority", "NEXUSAUTH_WORKBENCH_AUTH_AUTHORITY")]
    [InlineData("Auth:Audience", "NEXUSAUTH_WORKBENCH_AUTH_AUDIENCE")]
    [InlineData("Auth:ClientId", "NEXUSAUTH_WORKBENCH_AUTH_CLIENT_ID")]
    [InlineData("Auth:ClientSecret", "NEXUSAUTH_WORKBENCH_AUTH_CLIENT_SECRET")]
    [InlineData("Auth:RedirectUri", "NEXUSAUTH_WORKBENCH_AUTH_REDIRECT_URI")]
    [InlineData("Auth:PostLogoutRedirectUri", "NEXUSAUTH_WORKBENCH_AUTH_POST_LOGOUT_REDIRECT_URI")]
    [InlineData("Auth:Scope", "NEXUSAUTH_WORKBENCH_AUTH_SCOPE")]
    public void Direct_mode_fails_startup_for_empty_required_authentication_configuration(string key, string environmentVariable)
    {
        using var factory = CreateFactory(false, builder => builder.UseSetting(key, ""));

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(key, exception.ToString());
        Assert.Contains(environmentVariable, exception.ToString());
    }

    [Theory]
    [InlineData("Auth:Authority")]
    [InlineData("Auth:Audience")]
    [InlineData("Auth:ClientId")]
    [InlineData("Auth:ClientSecret")]
    public void Gateway_still_requires_validation_and_bootstrap_configuration(string key)
    {
        using var factory = CreateFactory(true, builder => builder.UseSetting(key, ""));

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(key, exception.ToString());
    }

    [Fact]
    public void Invalid_gateway_mode_value_fails_startup()
    {
        using var factory = CreateFactory(false, builder => builder.UseSetting("Gateway:Enabled", "not-a-boolean"));

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("NEXUSAUTH_WORKBENCH_GATEWAY_ENABLED", exception.ToString());
    }

    private static WebApplicationFactory<WorkbenchApiModule> CreateFactory(
        bool gateway, Action<IWebHostBuilder>? configure = null, SecurityKey? signingKey = null)
    {
        return new WebApplicationFactory<WorkbenchApiModule>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing")
                .UseSetting("Gateway:Enabled", gateway.ToString())
                .UseSetting("Auth:Authority", Authority)
                .UseSetting("Auth:Audience", Audience)
                .UseSetting("Auth:ClientId", "nexusauth.workbench")
                .UseSetting("Auth:ClientSecret", "test-only-secret")
                .UseSetting("Auth:RedirectUri", "https://dashboard.example.test/signin-oidc")
                .UseSetting("Auth:PostLogoutRedirectUri", "https://dashboard.example.test/")
                .UseSetting("Auth:Scope", "openid profile nexusauth.workbench.api");
            // Pin discovery and signing keys locally so authentication tests never contact a provider.
            builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(
                WorkbenchAuthenticationDefaults.BearerScheme, options =>
                {
                    var configuration = new OpenIdConnectConfiguration { Issuer = Authority };
                    if (signingKey is not null)
                        configuration.SigningKeys.Add(signingKey);
                    options.Configuration = configuration;
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                }));
            configure?.Invoke(builder);
        });
    }
}
