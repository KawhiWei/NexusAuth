using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
            .UseSetting("BootstrapAdmin:Password", string.Empty));
    }

    [Fact]
    public async Task Discovery_endpoint_is_available_when_the_real_provider_host_starts()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration");

        response.EnsureSuccessStatusCode();
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
