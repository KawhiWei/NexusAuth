using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NexusAuth.Workbench.Api;
using NexusAuth.Domain.AggregateRoots.ApiResources;
using NexusAuth.Domain.AggregateRoots.OAuthClients;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;
using Xunit;

namespace NexusAuth.Workbench.Api.IntegrationTests;

public sealed class WorkbenchBootstrapValidationTests
{
    [Fact]
    public async Task Bootstrap_preserves_its_resource_and_client_when_switching_modes()
    {
        OAuthClient? client = null;
        ApiResource? resource = null;
        var linkedResources = new Dictionary<Guid, List<Guid>>();
        var secretWrites = 0;
        var clientRepository = CreateRepository<IOAuthClientRepository>((method, args) =>
        {
            switch (method.Name)
            {
                case "FindByClientIdAsync": return Task.FromResult(client);
                case "AddAsync": client = (OAuthClient)args![0]!; return Task.CompletedTask;
                case "UpdateAsync": return Task.CompletedTask;
                case "ReplaceSharedSecretAsync":
                    client!.ClientSecrets.Clear();
                    client.ClientSecrets.Add((OAuthClientSecret)args![1]!);
                    secretWrites++;
                    return Task.CompletedTask;
                default: throw new NotSupportedException(method.Name);
            }
        });
        var resourceRepository = CreateRepository<IApiResourceRepository>((method, args) => method.Name switch
        {
            "FindByNameAsync" => Task.FromResult(resource),
            "AddAsync" => AddResource((ApiResource)args![0]!),
            "UpdateAsync" => Task.CompletedTask,
            _ => throw new NotSupportedException(method.Name)
        });
        Task AddResource(ApiResource value) { resource = value; return Task.CompletedTask; }
        var associations = CreateRepository<IClientApiResourceRepository>((method, args) =>
        {
            if (method.Name == "GetApiResourceIdsByClientIdsAsync")
                return Task.FromResult(linkedResources);
            if (method.Name == "AddAsync")
            {
                linkedResources[client!.Id] = [resource!.Id];
                return Task.CompletedTask;
            }
            throw new NotSupportedException(method.Name);
        });
        using var provider = new ServiceCollection()
            .AddSingleton(clientRepository).AddSingleton(resourceRepository).AddSingleton(associations)
            .BuildServiceProvider();
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:ClientId"] = "workbench", ["Auth:ClientSecret"] = "bootstrap-secret",
            ["Auth:Audience"] = "workbench.api", ["Auth:RedirectUri"] = "https://app.test/signin-oidc",
            ["Auth:PostLogoutRedirectUri"] = "https://app.test/"
        });
        var service = new WorkbenchClientCredentialHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(), configuration,
            Options.Create(new WorkbenchBootstrapOptions
            {
                ResourceName = "workbench.api", ResourceDisplayName = "Workbench API", ClientName = "Workbench"
            }), new ProductionEnvironment(), NullLogger<WorkbenchClientCredentialHostedService>.Instance);

        await service.StartAsync(default);
        var clientId = client!.Id;
        var resourceId = resource!.Id;
        Assert.Equal(new[] { "authorization_code", "refresh_token" }, client.AllowedGrantTypes);

        configuration["Gateway:Enabled"] = "true";
        configuration["Auth:RedirectUri"] = "";
        configuration["Auth:PostLogoutRedirectUri"] = "";
        await service.StartAsync(default);
        await service.StartAsync(default);
        Assert.Equal(clientId, client.Id);
        Assert.Equal(resourceId, resource.Id);
        Assert.Empty(client.RedirectUris);
        Assert.Empty(client.PostLogoutRedirectUris);
        Assert.Empty(client.AllowedGrantTypes);
        Assert.Equal(new[] { "workbench.api" }, client.AllowedScopes);
        Assert.Single(linkedResources[clientId]);
        Assert.Equal(1, secretWrites);

        configuration["Gateway:Enabled"] = "false";
        configuration["Auth:RedirectUri"] = "https://app.test/signin-oidc";
        configuration["Auth:PostLogoutRedirectUri"] = "https://app.test/";
        await service.StartAsync(default);
        Assert.Equal(new[] { "authorization_code", "refresh_token" }, client.AllowedGrantTypes);
        Assert.Single(client.RedirectUris);
        Assert.Equal(1, secretWrites);
    }

    private static T CreateRepository<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, RepositoryProxy>();
        ((RepositoryProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class RepositoryProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }

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
