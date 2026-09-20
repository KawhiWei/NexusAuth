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
        var audience = configuration["Auth:Audience"]?.Trim();
        var gatewayEnabled = configuration.IsWorkbenchGatewayEnabled();
        var options = bootstrapOptions.Value;
        var resourceName = options.ResourceName?.Trim();

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)
            || (!gatewayEnabled && (string.IsNullOrWhiteSpace(redirectUri) || string.IsNullOrWhiteSpace(postLogoutRedirectUri)))
            || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(resourceName))
        {
            throw new InvalidOperationException(
                "Workbench initialization requires Auth:ClientId, Auth:ClientSecret, Auth:RedirectUri, " +
                "Auth:PostLogoutRedirectUri (non-gateway mode), Auth:Audience and Bootstrap:ResourceName.");
        }

        RequireValue(options.ResourceDisplayName, "Bootstrap:ResourceDisplayName");
        RequireValue(options.ClientName, "Bootstrap:ClientName");
        var managedScopes = (gatewayEnabled ? Array.Empty<string>() : StaticClientScopes)
            .Append(audience)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] redirectUris = gatewayEnabled ? [] : [redirectUri!];
        string[] postLogoutRedirectUris = gatewayEnabled ? [] : [postLogoutRedirectUri!];
        // The gateway owns its login client; keep this application's registration without enabling new grants.
        string[] grantTypes = gatewayEnabled ? [] : ["authorization_code", "refresh_token"];

        using var scope = scopeFactory.CreateScope();
        var clientRepository = scope.ServiceProvider.GetRequiredService<IOAuthClientRepository>();
        var resourceRepository = scope.ServiceProvider.GetRequiredService<IApiResourceRepository>();
        var clientResourceRepository = scope.ServiceProvider.GetRequiredService<IClientApiResourceRepository>();

        var resource = await EnsureResourceAsync(
            resourceRepository,
            resourceName,
            options.ResourceDisplayName,
            audience,
            options.ResourceDescription,
            cancellationToken);

        var client = await clientRepository.FindByClientIdAsync(clientId, cancellationToken);
        if (client is null)
        {
            client = OAuthClient.Create(
                clientId,
                RequireValue(options.ClientName, "Bootstrap:ClientName"),
                options.ClientDescription,
                redirectUris,
                postLogoutRedirectUris,
                managedScopes,
                grantTypes,
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
                redirectUris,
                postLogoutRedirectUris,
                managedScopes,
                grantTypes,
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
        if (!existingResourceIds.Contains(resource.Id))
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

    private static string RequireValue(string? value, string configurationKey)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{configurationKey} must be configured.")
            : value.Trim();
    }
}
