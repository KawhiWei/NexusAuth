using NexusAuth.Domain.AggregateRoots.OAuthClients;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Application.Services;

public class ClientService : IClientService
{
    private readonly IOAuthClientRepository _clientRepository;

    public ClientService(IOAuthClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    public async Task<OAuthClient> RegisterClientAsync(
        string clientId,
        string rawClientSecret,
        string clientName,
        string? description = null,
        IEnumerable<string>? redirectUris = null,
        IEnumerable<string>? allowedScopes = null,
        IEnumerable<string>? allowedGrantTypes = null,
        bool requirePkce = true,
        CancellationToken ct = default)
    {
        var existing = await _clientRepository.FindByClientIdAsync(clientId, ct);
        if (existing is not null)
            throw new InvalidOperationException($"ClientId '{clientId}' is already registered.");

        var client = OAuthClient.Create(
            clientId,
            rawClientSecret,
            clientName,
            description,
            redirectUris,
            allowedScopes,
            allowedGrantTypes,
            requirePkce);

        await _clientRepository.AddAsync(client, ct);

        return client;
    }

    public async Task<OAuthClient?> ValidateClientAsync(
        string clientId,
        string rawClientSecret,
        CancellationToken ct = default)
    {
        var client = await _clientRepository.FindByClientIdAsync(clientId, ct);

        if (client is null || !client.IsActive)
            return null;

        return client.VerifyClientSecret(rawClientSecret) ? client : null;
    }

    public async Task<ClientValidationResult> ValidateClientForAuthorizationAsync(
        string clientId,
        string redirectUri,
        string grantType,
        string? codeChallenge = null,
        CancellationToken ct = default)
    {
        var client = await _clientRepository.FindByClientIdAsync(clientId, ct);

        if (client is null || !client.IsActive)
            return ClientValidationResult.Failure("invalid_client", "Client not found or inactive.");

        if (!client.IsValidRedirectUri(redirectUri))
            return ClientValidationResult.Failure("invalid_request", "Invalid redirect_uri.");

        if (!client.IsGrantTypeAllowed(grantType))
            return ClientValidationResult.Failure("unauthorized_client",
                $"Client is not allowed to use {grantType} grant type.");

        if (client.RequirePkce && string.IsNullOrWhiteSpace(codeChallenge))
            return ClientValidationResult.Failure("invalid_request",
                "code_challenge is required for this client (PKCE).");

        return ClientValidationResult.Success();
    }
}
