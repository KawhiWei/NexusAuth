using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NexusAuth.Workbench.Api;
using Xunit;

namespace NexusAuth.Workbench.Api.IntegrationTests;

public sealed class WorkbenchBootstrapValidationTests
{
    [Fact]
    public async Task Missing_required_configuration_fails_instead_of_skipping_registration()
    {
        var service = new WorkbenchClientCredentialHostedService(
            null!, new ConfigurationManager(), Options.Create(new WorkbenchBootstrapOptions()),
            new ProductionEnvironment(), NullLogger<WorkbenchClientCredentialHostedService>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(default));

        Assert.Contains("Bootstrap:ResourceName", exception.Message);
        Assert.Contains("Auth:ClientSecret", exception.Message);
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
