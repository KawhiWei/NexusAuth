using NexusAuth.Domain.AggregateRoots.OAuthClients;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Application.Services;

public class ClientService : IClientService
{
    private readonly IOAuthClientRepository _clientRepository;
    private readonly IApiResourceRepository _apiResourceRepository;

    public ClientService(
        IOAuthClientRepository clientRepository,
        IApiResourceRepository apiResourceRepository)
    {
        _clientRepository = clientRepository;
        _apiResourceRepository = apiResourceRepository;
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

    public async Task<ClientAuthenticationResult> AuthenticateClientAsync(
        string clientId,
        string? rawClientSecret,
        bool requireSecret,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return ClientAuthenticationResult.Failure("invalid_client", "client_id is required.");

        var client = await _clientRepository.FindByClientIdAsync(clientId, ct);
        if (client is null || !client.IsActive)
            return ClientAuthenticationResult.Failure("invalid_client", "Client not found or inactive.");

        if (requireSecret)
        {
            if (string.IsNullOrWhiteSpace(rawClientSecret))
                return ClientAuthenticationResult.Failure("invalid_client", "client_secret is required.");

            if (!client.VerifyClientSecret(rawClientSecret))
                return ClientAuthenticationResult.Failure("invalid_client", "Invalid client secret.");
        }

        return ClientAuthenticationResult.Success(client);
    }

    public async Task<ScopeValidationResult> ValidateScopesAsync(
        string clientId,
        string scope,
        bool allowIdentityScopes,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return ScopeValidationResult.Failure("invalid_scope", "scope is required.");

        var client = await _clientRepository.FindByClientIdAsync(clientId, ct);
        if (client is null || !client.IsActive)
            return ScopeValidationResult.Failure("invalid_client", "Client not found or inactive.");

        var requestedScopes = scope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var requestedScope in requestedScopes)
        {
            if (!client.AllowedScopes.Contains(requestedScope, StringComparer.Ordinal))
                return ScopeValidationResult.Failure("invalid_scope", $"Scope '{requestedScope}' is not allowed for this client.");

            if (allowIdentityScopes && IsIdentityScope(requestedScope))
                continue;

            var resource = await _apiResourceRepository.FindByNameAsync(requestedScope, ct);
            if (resource is null || !resource.IsActive)
                return ScopeValidationResult.Failure("invalid_scope", $"Scope '{requestedScope}' does not correspond to an active resource.");
        }

        return ScopeValidationResult.Success(string.Join(' ', requestedScopes));
    }

    private static bool IsIdentityScope(string scope)
    {
        return string.Equals(scope, "openid", StringComparison.Ordinal)
            || string.Equals(scope, "profile", StringComparison.Ordinal)
            || string.Equals(scope, "email", StringComparison.Ordinal)
            || string.Equals(scope, "phone", StringComparison.Ordinal)
            || string.Equals(scope, "address", StringComparison.Ordinal)
            || string.Equals(scope, "offline_access", StringComparison.Ordinal);
    }
}
