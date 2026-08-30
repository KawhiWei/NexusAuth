using Microsoft.Extensions.Hosting;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Workbench.Api;

/// <summary>
/// Keeps the system Workbench client's credential aligned with the API's configured secret.
/// </summary>
public sealed class WorkbenchClientCredentialHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<WorkbenchClientCredentialHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            logger.LogDebug("Workbench OAuth client credential synchronization is skipped in the test host.");
            return;
        }

        var clientId = configuration["Auth:ClientId"]?.Trim();
        var clientSecret = configuration["Auth:ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("Auth:ClientId and Auth:ClientSecret must be configured.");

        using var scope = scopeFactory.CreateScope();
        var clientRepository = scope.ServiceProvider.GetRequiredService<IOAuthClientRepository>();
        var client = await clientRepository.FindByClientIdAsync(clientId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"OAuth client '{clientId}' was not found. Initialize the Workbench client before starting the API.");

        if (!client.AllowsClientSecretAuthentication())
            throw new InvalidOperationException(
                $"OAuth client '{clientId}' does not support client-secret authentication.");

        if (client.VerifyClientSecret(clientSecret))
        {
            logger.LogDebug("Workbench OAuth client credential is already synchronized.");
            return;
        }

        foreach (var secret in client.ClientSecrets.Where(secret =>
                     secret.IsActive && string.Equals(secret.Type, OAuthClientSecret.TypeSharedSecret, StringComparison.Ordinal)))
        {
            secret.Disable();
        }

        client.Update(clientSecrets: [
            OAuthClientSecret.CreateSharedSecret(
                client.Id,
                clientSecret,
                "Managed by the Workbench Auth:ClientSecret configuration")
        ]);
        await clientRepository.UpdateAsync(client, cancellationToken);

        logger.LogInformation("Workbench OAuth client credential was synchronized from configuration.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
