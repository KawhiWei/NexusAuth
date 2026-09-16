using Microsoft.Extensions.Configuration;
using NexusAuth.Workbench.Api;
using Xunit;

namespace NexusAuth.Workbench.Api.IntegrationTests;

[CollectionDefinition("Workbench environment", DisableParallelization = true)]
public sealed class WorkbenchEnvironmentCollection;

[Collection("Workbench environment")]
public sealed class WorkbenchEnvironmentConfigurationTests
{
    [Fact]
    public void Independent_bootstrap_variables_bind_to_the_workbench_options()
    {
        var values = new Dictionary<string, string>
        {
            ["NEXUSAUTH_WORKBENCH_BOOTSTRAP_RESOURCE_NAME"] = "nexusauth.workbench.api",
            ["NEXUSAUTH_WORKBENCH_BOOTSTRAP_RESOURCE_DISPLAY_NAME"] = "Workbench API",
            ["NEXUSAUTH_WORKBENCH_BOOTSTRAP_CLIENT_NAME"] = "Workbench",
            ["NEXUSAUTH_WORKBENCH_BOOTSTRAP_RESOURCE_DESCRIPTION"] = "Service description",
            ["NEXUSAUTH_WORKBENCH_BOOTSTRAP_CLIENT_DESCRIPTION"] = "Application description",
        };
        var originalValues = values.Keys.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var (name, value) in values)
                Environment.SetEnvironmentVariable(name, value);
            using var configuration = new ConfigurationManager();
            configuration.AddWorkbenchEnvironmentVariables();

            var options = configuration.GetSection("Bootstrap").Get<WorkbenchBootstrapOptions>();

            Assert.Equal("nexusauth.workbench.api", options!.ResourceName);
            Assert.Equal("Workbench API", options.ResourceDisplayName);
            Assert.Equal("Workbench", options.ClientName);
            Assert.Equal("Service description", options.ResourceDescription);
            Assert.Equal("Application description", options.ClientDescription);
        }
        finally
        {
            foreach (var (name, value) in originalValues)
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}
