using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace NexusAuth.Host.IntegrationTests;

public sealed class ProviderHostTests : IClassFixture<WebApplicationFactory<AppWebModule>>
{
    private readonly WebApplicationFactory<AppWebModule> factory;

    public ProviderHostTests(WebApplicationFactory<AppWebModule> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("BootstrapAdmin:Username", string.Empty)
            .UseSetting("BootstrapAdmin:Password", string.Empty)
            .ConfigureTestServices(services => services.Remove(
                services.Single(descriptor => descriptor.ImplementationType == typeof(BootstrapAdminHostedService)))));
    }

    [Fact]
    public async Task Discovery_endpoint_is_available_when_the_real_provider_host_starts()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Jwks_endpoint_publishes_public_signing_keys()
    {
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/.well-known/jwks.json"));
        var keys = document.RootElement.GetProperty("keys");

        Assert.NotEmpty(keys.EnumerateArray());
        foreach (var key in keys.EnumerateArray())
        {
            Assert.True(key.TryGetProperty("kid", out _));
            Assert.False(key.TryGetProperty("d", out _));
        }
    }

    [Fact]
    public async Task Change_password_page_requires_an_authenticated_provider_session()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/account/change-password");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Registration_page_returns_not_found_when_self_registration_is_disabled()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/account/register");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Login_page_hides_registration_link_when_self_registration_is_disabled()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/account/login");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/Account/Register", page);
        Assert.DoesNotContain("创建账号", page);
    }

    [Fact]
    public async Task Registration_post_returns_not_found_when_self_registration_is_disabled()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.PostAsync(
            "/account/register",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Username"] = "disabled-user",
                ["Nickname"] = "Disabled User",
                ["Email"] = "disabled@example.com",
                ["Password"] = "Password123!",
                ["ConfirmPassword"] = "Password123!",
            }));

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Registration_page_is_available_to_anonymous_users_when_self_registration_is_enabled()
    {
        using var enabledFactory = factory.WithWebHostBuilder(builder => builder
            .UseSetting("SelfRegistration:Enabled", "true"));
        using var client = enabledFactory.CreateClient();

        var response = await client.GetAsync("/account/register");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadAsStringAsync();
        Assert.Contains("创建账号", page);
        Assert.Contains("登录账号", page);
        Assert.Contains("确认密码", page);
    }

    [Fact]
    public async Task Login_page_shows_registration_link_when_self_registration_is_enabled()
    {
        using var enabledFactory = factory.WithWebHostBuilder(builder => builder
            .UseSetting("SelfRegistration:Enabled", "true"));
        using var client = enabledFactory.CreateClient();

        var response = await client.GetAsync("/account/login");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadAsStringAsync();
        Assert.Contains("/Account/Register", page);
        Assert.Contains("创建账号", page);
    }

    [Fact]
    public async Task Login_page_hides_passkey_action_when_webauthn_is_disabled()
    {
        using var client = factory.CreateClient();

        var page = await client.GetStringAsync("/account/login");

        Assert.DoesNotContain("Sign in with a passkey", page);
    }

    [Fact]
    public async Task Enabled_webauthn_shows_passkey_login_action()
    {
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("WebAuthn:Enabled", "true"));
        using var client = enabledFactory.CreateClient();

        var loginPage = await client.GetStringAsync("/account/login");

        Assert.Contains("Sign in with a passkey", loginPage);
    }

    [Fact]
    public async Task Passkey_enrollment_requires_an_authenticated_session()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/account/PasskeyEnrollment?token=invalid");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Invalid_authorization_request_is_displayed_on_the_provider_error_page()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/connect/authorize?response_type=code");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.StartsWith("/oauth/error", location);
        Assert.Contains("error=invalid_request", location);
    }
}
