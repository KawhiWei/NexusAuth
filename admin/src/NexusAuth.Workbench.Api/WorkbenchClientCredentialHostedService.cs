using Microsoft.Extensions.Options;
using NexusAuth.Domain.AggregateRoots.ApiResources;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Workbench.Api;

/// <summary>
/// Idempotently provisions the Workbench OAuth resource, client, associations, and secret from configuration.
/// </summary>
public sealed class WorkbenchClientCredentialHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IOptions<WorkbenchBootstrapOptions> bootstrapOptions,
    IHostEnvironment environment,
    ILogger<WorkbenchClientCredentialHostedService> logger) : IHostedService
{
    private static readonly string[] StaticClientScopes = ["openid", "profile", "offline_access"];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            logger.LogDebug("Workbench OAuth bootstrap is skipped in the test host.");
            return;
        }

        var clientId = configuration["Auth:ClientId"]?.Trim();
        var clientSecret = configuration["Auth:ClientSecret"]?.Trim();
        var redirectUri = configuration["Auth:RedirectUri"]?.Trim();
        var postLogoutRedirectUri = configuration["Auth:PostLogoutRedirectUri"]?.Trim();
        var requestedScopes = ParseScopes(configuration["Auth:Scope"]);
        var audience = configuration["Auth:Audience"]?.Trim();
        var options = bootstrapOptions.Value;
        var resourceName = options.ResourceName?.Trim();

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)
            || string.IsNullOrWhiteSpace(redirectUri) || string.IsNullOrWhiteSpace(postLogoutRedirectUri)
            || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(resourceName))
        {
            logger.LogError("NexusAuth Workbench Initialization failed, environment variable has a configured value");
            logger.LogError("NexusAuth Workbench 初始化失败，环境变量存在为配置的值。");
            return;
        }

        var configuredServiceScopes = string.IsNullOrWhiteSpace(options.AllowedScopes)
            ? [audience]
            : ParseScopes(options.AllowedScopes);
        var managedScopes = StaticClientScopes
            .Concat(requestedScopes)
            .Concat(configuredServiceScopes)
            .Append(audience)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        using var scope = scopeFactory.CreateScope();
        var clientRepository = scope.ServiceProvider.GetRequiredService<IOAuthClientRepository>();
        var resourceRepository = scope.ServiceProvider.GetRequiredService<IApiResourceRepository>();
        var clientResourceRepository = scope.ServiceProvider.GetRequiredService<IClientApiResourceRepository>();

        await EnsureResourceAsync(
            resourceRepository,
            resourceName,
            options.ResourceDisplayName,
            audience,
            options.ResourceDescription,
            cancellationToken);

        var resources = await resourceRepository.FindByAudiencesAsync(configuredServiceScopes, cancellationToken);
        var foundAudiences = resources.Select(resource => resource.Audience).ToHashSet(StringComparer.Ordinal);
        var missingResourceScopes = configuredServiceScopes.Where(scope => !foundAudiences.Contains(scope)).ToArray();
        if (missingResourceScopes.Length > 0)
        {
            throw new InvalidOperationException(
                $"Bootstrap:AllowedScopes references service resources that do not exist: {string.Join(", ", missingResourceScopes)}.");
        }

        var client = await clientRepository.FindByClientIdAsync(clientId, cancellationToken);
        if (client is null)
        {
            client = OAuthClient.Create(
                clientId,
                RequireValue(options.ClientName, "Bootstrap:ClientName"),
                options.ClientDescription,
                [redirectUri],
                [postLogoutRedirectUri],
                managedScopes,
                ["authorization_code", "refresh_token"],
                requirePkce: true,
                tokenEndpointAuthMethod: OAuthClient.TokenEndpointAuthMethodClientSecretBasic);
            await clientRepository.AddAsync(client, cancellationToken);
            logger.LogInformation("Created Workbench OAuth client {ClientId} from configuration.", clientId);
        }
        else
        {
            client.Update(
                RequireValue(options.ClientName, "Bootstrap:ClientName"),
                options.ClientDescription,
                [redirectUri],
                [postLogoutRedirectUri],
                managedScopes,
                ["authorization_code", "refresh_token"],
                requirePkce: true,
                isActive: true,
                tokenEndpointAuthMethod: OAuthClient.TokenEndpointAuthMethodClientSecretBasic,
                jwks: string.Empty,
                jwksUri: string.Empty);
            await clientRepository.UpdateAsync(client, cancellationToken);
            logger.LogInformation("Updated Workbench OAuth client {ClientId} from configuration.", clientId);
        }

        var associatedResourceIds = await clientResourceRepository.GetApiResourceIdsByClientIdsAsync([client.Id], cancellationToken);
        var existingResourceIds = associatedResourceIds.GetValueOrDefault(client.Id, []);
        foreach (var resource in resources.Where(resource => !existingResourceIds.Contains(resource.Id)))
            await clientResourceRepository.AddAsync(ClientApiResource.Create(client.Id, resource.Id), cancellationToken);

        if (client.VerifyClientSecret(clientSecret))
        {
            logger.LogInformation("Workbench OAuth bootstrap completed; existing client credential is valid.");
        }
        else
        {
            await clientRepository.ReplaceSharedSecretAsync(
                client.Id,
                OAuthClientSecret.CreateSharedSecret(
                    client.Id,
                    clientSecret,
                    "Managed by the Workbench Auth:ClientSecret configuration"),
                cancellationToken);
            logger.LogInformation("Workbench OAuth bootstrap completed and synchronized the client credential.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<ApiResource> EnsureResourceAsync(
        IApiResourceRepository repository,
        string name,
        string? displayName,
        string audience,
        string? description,
        CancellationToken cancellationToken)
    {
        var normalizedDisplayName = RequireValue(displayName, $"resource '{name}' display name");
        var resource = await repository.FindByNameAsync(name, cancellationToken);
        if (resource is null)
        {
            resource = ApiResource.Create(name, normalizedDisplayName, audience, description);
            await repository.AddAsync(resource, cancellationToken);
            return resource;
        }

        resource.Update(normalizedDisplayName, audience, description, isActive: true);
        await repository.UpdateAsync(resource, cancellationToken);
        return resource;
    }

    private static string[] ParseScopes(string? scope)
    {
        return (scope ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string RequireValue(string? value, string configurationKey)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{configurationKey} must be configured.")
            : value.Trim();
    }
}
