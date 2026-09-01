using Luck.Logging.Serilog;
using NexusAuth.Application.Clients;
using NexusAuth.Application.Services.Security;
using NexusAuth.Application.Services.OIDC;

namespace NexusAuth.Application.Services.Authorization;

public class AuthorizationService(
    IAuthorizationCodeRepository codeRepository,
    IClientService clientService,
    ISecurityPolicyService securityPolicyService,
    ILogger<AuthorizationService> logger) : IAuthorizationService
{
    /// <summary>
    /// 1. 校验 scope
    /// 2. 解析 claims 参数
    /// </summary>
    public async Task<string> GenerateCodeAsync(
        Guid userId,
        string clientId,
        string redirectUri,
        string scope,
        string? codeChallenge = null,
        string? codeChallengeMethod = null,
        string? nonce = null,
        string? claimsJson = null,
        DateTimeOffset? authenticatedAt = null,
        string? acr = null,
        string? amr = null,
        CancellationToken ct = default)
    {
        var scopeValidation = await clientService.ValidateScopesAsync(clientId, scope, allowIdentityScopes: true, ct);
        if (!scopeValidation.IsSuccess)
        {
            LogAuthorizationFailure(clientId, "InvalidScope");
            throw new InvalidOperationException(scopeValidation.Error);
        }

        if (!string.IsNullOrWhiteSpace(claimsJson))
        {
            try
            {
                ParseRequestedClaims(claimsJson);
            }
            catch
            {
                LogAuthorizationFailure(clientId, "InvalidClaims");
                throw;
            }
        }

        var code = AuthorizationCode.Create(
            clientId,
            userId,
            redirectUri,
            scopeValidation.NormalizedScope!,
            codeChallenge,
            codeChallengeMethod,
            nonce,
            claimsJson,
            authenticatedAt,
            acr,
            amr);

        await codeRepository.AddAsync(code.Entity, ct);

        logger.LogLuckInformation(
            "Authorization code issued. UserId={UserId} ClientId={ClientId} Outcome={Outcome}",
            [userId, clientId, "AuthorizationCodeIssued"]);

        return code.RawCode;
    }

    /// <summary>
    /// </summary>
    public OidcRequestedClaims ParseRequestedClaims(string? claimsJson)
    {
        try
        {
            return OidcRequestedClaims.Parse(claimsJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("OIDC claims parameter is not valid JSON.", ex);
        }
    }

    /// <summary>
    /// 2. 校验 redirect_uri
    /// 3. 校验 PKCE
    /// 4. 将授权码标记为已消费
    /// </summary>
    public async Task<AuthorizationCodeResult> ValidateAndConsumeCodeAsync(
        string code,
        string clientId,
        string redirectUri,
        string? codeVerifier = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        var authCode = await codeRepository.FindByCodeAsync(code, ct);

        if (authCode is null)
            return ConsumeFailure(clientId, "AuthorizationCodeNotFound", "Invalid authorization code.");

        if (authCode.IsUsed)
            return ConsumeFailure(clientId, "AuthorizationCodeUsed", "Authorization code has already been used.");

        if (authCode.ExpiresAt <= DateTimeOffset.UtcNow)
            return ConsumeFailure(clientId, "AuthorizationCodeExpired", "Authorization code has expired.");

        if (!string.Equals(authCode.ClientId, clientId, StringComparison.Ordinal))
            return ConsumeFailure(clientId, "ClientMismatch", "Authorization code does not belong to the authenticated client.");

        if (authCode.RedirectUri != redirectUri)
            return ConsumeFailure(clientId, "RedirectUriMismatch", "Redirect URI mismatch.");

        var clientPolicy = securityPolicyService.CheckClient(authCode.ClientId);
        if (!clientPolicy.IsSuccess)
            return ConsumeFailure(clientId, "ClientPolicyRejected", clientPolicy.Error ?? "Client is blocked by security policy.");

        // PKCE verification
        if (authCode.CodeChallenge is not null)
        {
            if (string.IsNullOrWhiteSpace(codeVerifier))
                return ConsumeFailure(clientId, "MissingCodeVerifier", "Code verifier is required for PKCE.");

            if (!VerifyPkce(codeVerifier, authCode.CodeChallenge, authCode.CodeChallengeMethod))
                return ConsumeFailure(clientId, "PkceVerificationFailed", "PKCE verification failed.");
        }

        var consumedCode = await codeRepository.ConsumeAsync(code, clientId, ct);
        if (consumedCode is null)
            return ConsumeFailure(clientId, "AuthorizationCodeConsumeRace", "Authorization code has already been used or expired.");

        logger.LogLuckInformation(
            "Authorization code consumed. UserId={UserId} ClientId={ClientId} Outcome={Outcome}",
            [consumedCode.UserId, clientId, "AuthorizationCodeConsumed"]);

        return AuthorizationCodeResult.Success(
            consumedCode.UserId,
            consumedCode.ClientId,
            consumedCode.Scope,
            consumedCode.Nonce,
            consumedCode.ClaimsJson,
            consumedCode.AuthenticatedAt,
            consumedCode.Acr,
            consumedCode.Amr);
    }

    /// <summary>
    /// </summary>
    public async Task<ClientCredentialsResult> ValidateClientCredentialsAsync(
        ClientAuthenticationInput authentication,
        string scope,
        CancellationToken ct = default)
    {
        var result = await clientService.AuthenticateClientAsync(authentication, requireClientAuthentication: true, ct);
        if (!result.IsSuccess)
            return ClientCredentialsResult.Failure(result.Error ?? "Invalid client.");

        var client = result.Client!;
        var clientId = client.ClientId;

        var clientPolicy = securityPolicyService.CheckClient(clientId);
        if (!clientPolicy.IsSuccess)
            return ClientCredentialsResult.Failure(clientPolicy.Error ?? "Client is blocked by security policy.");

        if (!client.IsGrantTypeAllowed("client_credentials"))
            return ClientCredentialsResult.Failure("Client is not allowed to use client_credentials grant type.");

        var scopeValidation = await clientService.ValidateScopesAsync(clientId, scope, allowIdentityScopes: false, ct);
        if (!scopeValidation.IsSuccess)
            return ClientCredentialsResult.Failure(scopeValidation.Error ?? "Invalid scope.");

        return ClientCredentialsResult.Success(clientId, scopeValidation.NormalizedScope!);
    }

    private AuthorizationCodeResult ConsumeFailure(
        string? clientId,
        string reasonCode,
        string error)
    {
        LogAuthorizationFailure(clientId, reasonCode);
        return AuthorizationCodeResult.Failure(error);
    }

    private void LogAuthorizationFailure(string? clientId, string reasonCode)
    {
        logger.LogLuckWarning(
            "Authorization code operation failed. Reason={ReasonCode} Outcome={Outcome}",
            [reasonCode, reasonCode]);
    }

    private static bool VerifyPkce(string codeVerifier, string codeChallenge, string? codeChallengeMethod)
    {
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
    string? Nonce,
    string? ClaimsJson,
    DateTimeOffset? AuthenticatedAt,
    string? Acr,
    string? Amr,
    string? Error)
{
    public static AuthorizationCodeResult Success(
        Guid userId,
        string clientId,
        string scope,
        string? nonce,
        string? claimsJson,
        DateTimeOffset? authenticatedAt,
        string? acr,
        string? amr)
        => new(true, userId, clientId, scope, nonce, claimsJson, authenticatedAt, acr, amr, null);

    public static AuthorizationCodeResult Failure(string error)
        => new(false, Guid.Empty, string.Empty, string.Empty, null, null, null, null, null, error);
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
