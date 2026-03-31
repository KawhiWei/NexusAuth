using System.Security.Cryptography;
using System.Text;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Application.Services;

public class AuthorizationService : IAuthorizationService
{
    private readonly IAuthorizationCodeRepository _codeRepository;
    private readonly IOAuthClientRepository _clientRepository;
    private readonly IApiResourceRepository _apiResourceRepository;

    public AuthorizationService(
        IAuthorizationCodeRepository codeRepository,
        IOAuthClientRepository clientRepository,
        IApiResourceRepository apiResourceRepository)
    {
        _codeRepository = codeRepository;
        _clientRepository = clientRepository;
        _apiResourceRepository = apiResourceRepository;
    }

    public async Task<string> GenerateCodeAsync(
        Guid userId,
        string clientId,
        string redirectUri,
        string scope,
        string? codeChallenge = null,
        string? codeChallengeMethod = null,
        CancellationToken ct = default)
    {
        var code = AuthorizationCode.Create(
            clientId,
            userId,
            redirectUri,
            scope,
            codeChallenge,
            codeChallengeMethod);

        await _codeRepository.AddAsync(code, ct);

        return code.Code;
    }

    public async Task<AuthorizationCodeResult> ValidateAndConsumeCodeAsync(
        string code,
        string redirectUri,
        string? codeVerifier = null,
        CancellationToken ct = default)
    {
        var authCode = await _codeRepository.FindByCodeAsync(code, ct);

        if (authCode is null)
            return AuthorizationCodeResult.Failure("Invalid authorization code.");

        if (authCode.IsUsed)
            return AuthorizationCodeResult.Failure("Authorization code has already been used.");

        if (authCode.ExpiresAt <= DateTimeOffset.UtcNow)
            return AuthorizationCodeResult.Failure("Authorization code has expired.");

        if (authCode.RedirectUri != redirectUri)
            return AuthorizationCodeResult.Failure("Redirect URI mismatch.");

        // PKCE verification
        if (authCode.CodeChallenge is not null)
        {
            if (string.IsNullOrWhiteSpace(codeVerifier))
                return AuthorizationCodeResult.Failure("Code verifier is required for PKCE.");

            if (!VerifyPkce(codeVerifier, authCode.CodeChallenge, authCode.CodeChallengeMethod))
                return AuthorizationCodeResult.Failure("PKCE verification failed.");
        }

        authCode.MarkAsUsed();
        await _codeRepository.MarkUsedAsync(authCode.Id, ct);

        return AuthorizationCodeResult.Success(authCode.UserId, authCode.ClientId, authCode.Scope);
    }

    public async Task<ClientCredentialsResult> ValidateClientCredentialsAsync(
        string clientId,
        string rawClientSecret,
        string scope,
        CancellationToken ct = default)
    {
        var client = await _clientRepository.FindByClientIdAsync(clientId, ct);

        if (client is null || !client.IsActive)
            return ClientCredentialsResult.Failure("Invalid client.");

        if (!client.VerifyClientSecret(rawClientSecret))
            return ClientCredentialsResult.Failure("Invalid client secret.");

        if (!client.IsGrantTypeAllowed("client_credentials"))
            return ClientCredentialsResult.Failure("Client is not allowed to use client_credentials grant type.");

        // Validate scopes
        var requestedScopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var s in requestedScopes)
        {
            if (!client.AllowedScopes.Contains(s))
                return ClientCredentialsResult.Failure($"Scope '{s}' is not allowed for this client.");

            var resource = await _apiResourceRepository.FindByNameAsync(s, ct);
            if (resource is null || !resource.IsActive)
                return ClientCredentialsResult.Failure($"Scope '{s}' does not correspond to an active API resource.");
        }

        return ClientCredentialsResult.Success(clientId, scope);
    }

    private static bool VerifyPkce(string codeVerifier, string codeChallenge, string? codeChallengeMethod)
    {
        if (codeChallengeMethod is null or "plain")
            return codeVerifier == codeChallenge;

        if (codeChallengeMethod == "S256")
        {
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            var computed = Convert.ToBase64String(hash)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
            return computed == codeChallenge;
        }

        return false;
    }
}

public record AuthorizationCodeResult(
    bool IsSuccess,
    Guid UserId,
    string ClientId,
    string Scope,
    string? Error)
{
    public static AuthorizationCodeResult Success(Guid userId, string clientId, string scope)
        => new(true, userId, clientId, scope, null);

    public static AuthorizationCodeResult Failure(string error)
        => new(false, Guid.Empty, string.Empty, string.Empty, error);
}

public record ClientCredentialsResult(
    bool IsSuccess,
    string ClientId,
    string Scope,
    string? Error)
{
    public static ClientCredentialsResult Success(string clientId, string scope)
        => new(true, clientId, scope, null);

    public static ClientCredentialsResult Failure(string error)
        => new(false, string.Empty, string.Empty, error);
}
