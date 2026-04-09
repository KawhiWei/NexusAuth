using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Application.Services;

public class AuthorizationService : IAuthorizationService
{
    private readonly IAuthorizationCodeRepository _codeRepository;
    private readonly IOAuthClientRepository _clientRepository;
    private readonly IClientService _clientService;
    private readonly ISecurityPolicyService _securityPolicyService;

    public AuthorizationService(
        IAuthorizationCodeRepository codeRepository,
        IOAuthClientRepository clientRepository,
        IClientService clientService,
        ISecurityPolicyService securityPolicyService)
    {
        _codeRepository = codeRepository;
        _clientRepository = clientRepository;
        _clientService = clientService;
        _securityPolicyService = securityPolicyService;
    }

    /// <summary>
    /// 生成并持久化授权码（authorization code）。
    /// 主要调用方：AuthorizeController。
    /// 主要职责：
    /// 1. 校验 scope
    /// 2. 解析 claims 参数
    /// 3. 生成一次性 authorization code 并入库
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
        var scopeValidation = await _clientService.ValidateScopesAsync(clientId, scope, allowIdentityScopes: true, ct);
        if (!scopeValidation.IsSuccess)
            throw new InvalidOperationException(scopeValidation.Error);

        if (!string.IsNullOrWhiteSpace(claimsJson))
        {
            // 中文注释：授权阶段先校验 claims JSON 格式，避免无效请求进入后续流程。
            ParseRequestedClaims(claimsJson);
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

        await _codeRepository.AddAsync(code, ct);

        return code.Code;
    }

    /// <summary>
    /// 解析并校验 OIDC claims 参数。
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
    /// 校验授权码并消费（一次性使用）。
    /// 主要调用方：TokenController 的 authorization_code 分支。
    /// 主要职责：
    /// 1. 校验 code 是否存在、是否过期、是否已被使用
    /// 2. 校验 redirect_uri
    /// 3. 校验 PKCE
    /// 4. 将授权码标记为已消费
    /// </summary>
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

        var clientPolicy = _securityPolicyService.CheckClient(authCode.ClientId);
        if (!clientPolicy.IsSuccess)
            return AuthorizationCodeResult.Failure(clientPolicy.Error ?? "Client is blocked by security policy.");

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

        return AuthorizationCodeResult.Success(
            authCode.UserId,
            authCode.ClientId,
            authCode.Scope,
            authCode.Nonce,
            authCode.ClaimsJson,
            authCode.AuthenticatedAt,
            authCode.Acr,
            authCode.Amr);
    }

    /// <summary>
    /// 校验 client_credentials 流程中的客户端与 scope。
    /// </summary>
    public async Task<ClientCredentialsResult> ValidateClientCredentialsAsync(
        ClientAuthenticationInput authentication,
        string scope,
        CancellationToken ct = default)
    {
        var result = await _clientService.AuthenticateClientAsync(authentication, requireClientAuthentication: true, ct);
        if (!result.IsSuccess)
            return ClientCredentialsResult.Failure(result.Error ?? "Invalid client.");

        var client = result.Client!;
        var clientId = client.ClientId;

        var clientPolicy = _securityPolicyService.CheckClient(clientId);
        if (!clientPolicy.IsSuccess)
            return ClientCredentialsResult.Failure(clientPolicy.Error ?? "Client is blocked by security policy.");

        if (!client.IsGrantTypeAllowed("client_credentials"))
            return ClientCredentialsResult.Failure("Client is not allowed to use client_credentials grant type.");

        var scopeValidation = await _clientService.ValidateScopesAsync(clientId, scope, allowIdentityScopes: false, ct);
        if (!scopeValidation.IsSuccess)
            return ClientCredentialsResult.Failure(scopeValidation.Error ?? "Invalid scope.");

        return ClientCredentialsResult.Success(clientId, scopeValidation.NormalizedScope!);
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

        // 中文注释：OAuth 2.1 风格下只接受 S256，这里明确拒绝 plain 和其它方法。
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
