using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NexusAuth.Application.Users;
using NexusAuth.Workbench.Api;
using Xunit;

namespace NexusAuth.Workbench.Api.IntegrationTests;

public sealed class WorkbenchApiCompositionTests : IClassFixture<WebApplicationFactory<WorkbenchApiModule>>
{
    private readonly WebApplicationFactory<WorkbenchApiModule> factory;

    public WorkbenchApiCompositionTests(WebApplicationFactory<WorkbenchApiModule> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Swagger_endpoint_is_available_when_the_real_workbench_host_starts()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task User_management_endpoint_requires_authentication()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/users");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void User_management_service_resolves_without_the_provider_token_signing_services()
    {
        using var scope = factory.Services.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IUserManagementService>();

        Assert.NotNull(service);
    }
}
