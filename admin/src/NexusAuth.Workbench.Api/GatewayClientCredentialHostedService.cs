using Microsoft.Extensions.Options;
using NexusAuth.Domain.AggregateRoots.OAuthClients;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Workbench.Api;

/// <summary>
/// Idempotently provisions the gateway confidential client and links it to configured API resources.
/// </summary>
public sealed class GatewayClientCredentialHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<GatewayBootstrapOptions> gatewayOptions,
    IHostEnvironment environment,
    ILogger<GatewayClientCredentialHostedService> logger) : IHostedService
{
    private static readonly string[] IdentityScopes = ["openid", "profile", "offline_access"];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = gatewayOptions.Value;
        if (!options.Enabled || environment.IsEnvironment("Testing"))
            return;

        var clientId = RequireValue(options.ClientId, "GatewayBootstrap:ClientId");
        var clientSecret = RequireValue(options.ClientSecret, "GatewayBootstrap:ClientSecret");
        var redirectUri = RequireValue(options.RedirectUri, "GatewayBootstrap:RedirectUri");
        var postLogoutRedirectUri = RequireValue(options.PostLogoutRedirectUri, "GatewayBootstrap:PostLogoutRedirectUri");
        var resourceAudiences = ParseScopes(RequireValue(options.AllowedScopes, "GatewayBootstrap:AllowedScopes"));

        using var scope = scopeFactory.CreateScope();
        var clientRepository = scope.ServiceProvider.GetRequiredService<IOAuthClientRepository>();
        var resourceRepository = scope.ServiceProvider.GetRequiredService<IApiResourceRepository>();
        var clientResourceRepository = scope.ServiceProvider.GetRequiredService<IClientApiResourceRepository>();

        var resources = await resourceRepository.FindByAudiencesAsync(resourceAudiences, cancellationToken);
        var foundAudiences = resources.Select(resource => resource.Audience).ToHashSet(StringComparer.Ordinal);
        var missingAudiences = resourceAudiences.Where(audience => !foundAudiences.Contains(audience)).ToArray();
        if (missingAudiences.Length > 0)
            throw new InvalidOperationException(
                $"GatewayBootstrap:AllowedScopes references service resources that do not exist: {string.Join(", ", missingAudiences)}.");

        var allowedScopes = IdentityScopes.Concat(resourceAudiences).Distinct(StringComparer.Ordinal).ToArray();
        var client = await clientRepository.FindByClientIdAsync(clientId, cancellationToken);
        if (client is null)
        {
            client = OAuthClient.Create(
                clientId,
                RequireValue(options.ClientName, "GatewayBootstrap:ClientName"),
                options.ClientDescription,
                [redirectUri],
                [postLogoutRedirectUri],
                allowedScopes,
                ["authorization_code", "refresh_token"],
                requirePkce: true,
                tokenEndpointAuthMethod: OAuthClient.TokenEndpointAuthMethodClientSecretBasic);
            await clientRepository.AddAsync(client, cancellationToken);
            logger.LogInformation("Created gateway OAuth client {ClientId} from configuration.", clientId);
        }
        else
        {
            client.Update(
                RequireValue(options.ClientName, "GatewayBootstrap:ClientName"),
                options.ClientDescription,
                [redirectUri],
                [postLogoutRedirectUri],
                allowedScopes,
                ["authorization_code", "refresh_token"],
                requirePkce: true,
                isActive: true,
                tokenEndpointAuthMethod: OAuthClient.TokenEndpointAuthMethodClientSecretBasic,
                jwks: string.Empty,
                jwksUri: string.Empty);
            await clientRepository.UpdateAsync(client, cancellationToken);
            logger.LogInformation("Updated gateway OAuth client {ClientId} from configuration.", clientId);
        }

        var existingResourceIds = (await clientResourceRepository.GetApiResourceIdsByClientIdsAsync([client.Id], cancellationToken))
            .GetValueOrDefault(client.Id, []);
        var requestedResourceIds = resources.Select(resource => resource.Id).ToHashSet();
        foreach (var resourceId in existingResourceIds.Where(resourceId => !requestedResourceIds.Contains(resourceId)))
            await clientResourceRepository.RemoveAsync(client.Id, resourceId, cancellationToken);
        foreach (var resource in resources.Where(resource => !existingResourceIds.Contains(resource.Id)))
            await clientResourceRepository.AddAsync(ClientApiResource.Create(client.Id, resource.Id), cancellationToken);

        if (!client.VerifyClientSecret(clientSecret))
        {
            await clientRepository.ReplaceSharedSecretAsync(
                client.Id,
                OAuthClientSecret.CreateSharedSecret(client.Id, clientSecret, "Managed by GatewayBootstrap:ClientSecret"),
                cancellationToken);
            logger.LogInformation("Synchronized gateway OAuth client credential for {ClientId}.", clientId);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string[] ParseScopes(string value) => value
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static string RequireValue(string? value, string configurationKey) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{configurationKey} must be configured.")
            : value.Trim();
}
