using Luck.Logging.Serilog;

namespace NexusAuth.Application.Clients;

public class ClientService(
    IOAuthClientRepository clientRepository,
    IApiResourceRepository apiResourceRepository,
    ITokenBlacklistRepository tokenBlacklistRepository,
    IOAuthClientSecretRepository clientSecretRepository,
    IClientApiResourceRepository clientApiResourceRepository,
    ILogger<ClientService> logger) : IClientService
{

    #region OAuth 授权服务 (Host API 使用)

    public async Task<OAuthClient> RegisterClientAsync(
        string clientId,
        string clientName,
        string? description = null,
        IEnumerable<string>? redirectUris = null,
        IEnumerable<string>? postLogoutRedirectUris = null,
        IEnumerable<string>? allowedScopes = null,
        IEnumerable<string>? allowedGrantTypes = null,
        bool requirePkce = true,
        string tokenEndpointAuthMethod = OAuthClient.TokenEndpointAuthMethodClientSecretBasic,
        IEnumerable<OAuthClientSecret>? clientSecrets = null,
        CancellationToken ct = default)
    {
        var existing = await clientRepository.FindByClientIdAsync(clientId, ct);
        if (existing is not null)
            throw new InvalidOperationException($"ClientId '{clientId}' is already registered.");

        var client = OAuthClient.Create(
            clientId,
            clientName,
            description,
            redirectUris,
            postLogoutRedirectUris,
            allowedScopes,
            allowedGrantTypes,
            requirePkce,
            tokenEndpointAuthMethod,
            clientSecrets);

        await clientRepository.AddAsync(client, ct);

        return client;
    }

    public async Task<OAuthClient?> ValidateClientAsync(
        string clientId,
        string rawClientSecret,
        CancellationToken ct = default)
    {
        var client = await clientRepository.FindByClientIdAsync(clientId, ct);

        if (client is null || !client.IsActive)
            return null;

        return client.VerifyClientSecret(rawClientSecret) ? client : null;
    }

    public async Task<ClientAuthenticationResult> AuthenticateClientAsync(
        ClientAuthenticationInput input,
        bool requireClientAuthentication,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.ClientId))
            return AuthenticationFailure(input.ClientId, "MissingClientId", "client_id is required.");

        var client = await clientRepository.FindByClientIdAsync(input.ClientId, ct);
        if (client is null || !client.IsActive)
            return AuthenticationFailure(input.ClientId, "ClientNotFoundOrInactive", "Client not found or inactive.");

        if (!requireClientAuthentication)
            return AuthenticationSuccess(client, "AuthenticationNotRequired");

        var hasAssertion = !string.IsNullOrWhiteSpace(input.ClientAssertion)
            || !string.IsNullOrWhiteSpace(input.ClientAssertionType);
        var hasSecret = !string.IsNullOrWhiteSpace(input.ClientSecret);

        if (hasAssertion && hasSecret)
            return AuthenticationFailure(client.ClientId, "MultipleMethods", "Multiple client authentication methods were supplied.");

        if (string.Equals(client.TokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodPrivateKeyJwt, StringComparison.Ordinal))
        {
            if (!string.Equals(input.ClientAssertionType, OAuthClient.ClientAssertionTypeJwtBearer, StringComparison.Ordinal))
                return AuthenticationFailure(client.ClientId, "WrongAuthMethod", "client_assertion_type is invalid for this client.");

            if (string.IsNullOrWhiteSpace(input.ClientAssertion))
                return AuthenticationFailure(client.ClientId, "MissingCredential", "client_assertion is required.");

            if (string.IsNullOrWhiteSpace(input.AssertionAudience))
                return AuthenticationFailure(client.ClientId, "MissingAssertionAudience", "assertion audience is required for private_key_jwt validation.");

            var assertionValidation = ClientPrivateKeyJwtValidator.Validate(input.ClientAssertion, client, input.AssertionAudience);
            if (!assertionValidation.IsSuccess)
                return AuthenticationFailure(client.ClientId, "InvalidAssertion", assertionValidation.Error ?? "Invalid client assertion.");

            if (string.IsNullOrWhiteSpace(assertionValidation.Jti) || assertionValidation.ExpiresAt is null)
                return AuthenticationFailure(client.ClientId, "IncompleteAssertion", "client_assertion metadata is incomplete.");

            var replayKey = BuildClientAssertionReplayKey(client.ClientId, assertionValidation.Jti);
            if (await tokenBlacklistRepository.ExistsActiveAsync(replayKey, DateTimeOffset.UtcNow, ct))
                return AuthenticationFailure(client.ClientId, "AssertionReplay", "client_assertion has already been used.");

            await tokenBlacklistRepository.AddAsync(
                TokenBlacklistEntry.Create(replayKey, "client_assertion", client.ClientId, assertionValidation.ExpiresAt.Value),
                ct);

            return AuthenticationSuccess(client, "AuthenticatedPrivateKeyJwt");
        }

        if (string.Equals(client.TokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodClientSecretJwt, StringComparison.Ordinal))
        {
            if (!string.Equals(input.ClientAssertionType, OAuthClient.ClientAssertionTypeJwtBearer, StringComparison.Ordinal))
                return AuthenticationFailure(client.ClientId, "WrongAuthMethod", "client_assertion_type is invalid for this client.");

            if (string.IsNullOrWhiteSpace(input.ClientAssertion))
                return AuthenticationFailure(client.ClientId, "MissingCredential", "client_assertion is required.");

            if (string.IsNullOrWhiteSpace(input.AssertionAudience))
                return AuthenticationFailure(client.ClientId, "MissingAssertionAudience", "assertion audience is required for client_secret_jwt validation.");

            var assertionValidation = ClientSecretJwtValidator.Validate(input.ClientAssertion, client, input.AssertionAudience);
            if (!assertionValidation.IsSuccess)
                return AuthenticationFailure(client.ClientId, "InvalidAssertion", assertionValidation.Error ?? "Invalid client assertion.");

            if (string.IsNullOrWhiteSpace(assertionValidation.Jti) || assertionValidation.ExpiresAt is null)
                return AuthenticationFailure(client.ClientId, "IncompleteAssertion", "client_assertion metadata is incomplete.");

            var replayKey = BuildClientAssertionReplayKey(client.ClientId, assertionValidation.Jti);
            if (await tokenBlacklistRepository.ExistsActiveAsync(replayKey, DateTimeOffset.UtcNow, ct))
                return AuthenticationFailure(client.ClientId, "AssertionReplay", "client_assertion has already been used.");

            await tokenBlacklistRepository.AddAsync(
                TokenBlacklistEntry.Create(replayKey, "client_assertion", client.ClientId, assertionValidation.ExpiresAt.Value),
                ct);

            return AuthenticationSuccess(client, "AuthenticatedClientSecretJwt");
        }

        if (hasAssertion)
            return AuthenticationFailure(client.ClientId, "WrongAuthMethod", "This client requires client secret authentication.");

        if (string.Equals(client.TokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodClientSecretBasic, StringComparison.Ordinal)
            && !string.Equals(input.TokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodClientSecretBasic, StringComparison.Ordinal))
        {
            return AuthenticationFailure(client.ClientId, "WrongAuthMethod", "Client must authenticate with client_secret_basic.");
        }

        if (string.Equals(client.TokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodClientSecretPost, StringComparison.Ordinal)
            && !string.Equals(input.TokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodClientSecretPost, StringComparison.Ordinal))
        {
            return AuthenticationFailure(client.ClientId, "WrongAuthMethod", "Client must authenticate with client_secret_post.");
        }

        if (string.IsNullOrWhiteSpace(input.ClientSecret))
            return AuthenticationFailure(client.ClientId, "MissingCredential", "client_secret is required.");

        if (!client.VerifyClientSecret(input.ClientSecret))
            return AuthenticationFailure(client.ClientId, "InvalidCredential", "Invalid client secret.");

        return AuthenticationSuccess(client, "AuthenticatedClientSecret");
    }

    public async Task<ClientValidationResult> ValidateClientForAuthorizationAsync(
        string clientId,
        string redirectUri,
        string grantType,
        string? codeChallenge = null,
        string? codeChallengeMethod = null,
        CancellationToken ct = default)
    {
        var client = await clientRepository.FindByClientIdAsync(clientId, ct);

        if (client is null || !client.IsActive)
            return ClientValidationResult.Failure("invalid_client", "Client not found or inactive.");

        if (!client.IsValidRedirectUri(redirectUri))
            return ClientValidationResult.Failure("invalid_request", "Invalid redirect_uri.");

        if (!client.IsGrantTypeAllowed(grantType))
            return ClientValidationResult.Failure("unauthorized_client",
                $"Client is not allowed to use {grantType} grant type.",
                redirectUriValidated: true);

        if (string.Equals(grantType, "authorization_code", StringComparison.OrdinalIgnoreCase))
        {
            if (client.RequirePkce && string.IsNullOrWhiteSpace(codeChallenge))
                return ClientValidationResult.Failure("invalid_request", "code_challenge is required for this client.", redirectUriValidated: true);

            if (!string.IsNullOrWhiteSpace(codeChallenge)
                && !string.Equals(codeChallengeMethod, "S256", StringComparison.Ordinal))
            {
                return ClientValidationResult.Failure("invalid_request", "code_challenge_method must be S256.", redirectUriValidated: true);
            }

            if (string.IsNullOrWhiteSpace(codeChallenge)
                && !string.IsNullOrWhiteSpace(codeChallengeMethod))
            {
                return ClientValidationResult.Failure("invalid_request", "code_challenge_method requires code_challenge.", redirectUriValidated: true);
            }
        }

        return ClientValidationResult.Success();
    }

    public async Task<ClientValidationResult> ValidateClientRedirectUriAsync(
        string clientId,
        string redirectUri,
        CancellationToken ct = default)
    {
        var client = await clientRepository.FindByClientIdAsync(clientId, ct);
        if (client is null || !client.IsActive)
            return ClientValidationResult.Failure("invalid_client", "Client not found or inactive.");

        if (!client.IsValidRedirectUri(redirectUri))
            return ClientValidationResult.Failure("invalid_request", "Invalid redirect_uri.");

        return ClientValidationResult.Success();
    }

    public async Task<ClientAuthenticationResult> AuthenticateClientAsync(
        string clientId,
        string? rawClientSecret,
        bool requireSecret,
        CancellationToken ct = default)
    {
        var input = new ClientAuthenticationInput(
            clientId,
            rawClientSecret,
            null,
            null,
            null,
            OAuthClient.TokenEndpointAuthMethodClientSecretPost);
        return await AuthenticateClientAsync(input, requireSecret, ct);
    }

    public async Task<ClientAuthenticationResult> AuthenticateClientForPostLogoutAsync(
        string clientId,
        string? postLogoutRedirectUri,
        CancellationToken ct = default)
    {
        var client = await clientRepository.FindByClientIdAsync(clientId, ct);
        if (client is null || !client.IsActive)
            return ClientAuthenticationResult.Failure("invalid_client", "Client not found or inactive.");

        if (!string.IsNullOrWhiteSpace(postLogoutRedirectUri)
            && !client.IsValidPostLogoutRedirectUri(postLogoutRedirectUri))
        {
            return ClientAuthenticationResult.Failure("invalid_request", "post_logout_redirect_uri is not registered for this client.");
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

        var client = await clientRepository.FindByClientIdAsync(clientId, ct);
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

            var resource = await apiResourceRepository.FindByAudienceAsync(requestedScope, ct);
            if (resource is null || !resource.IsActive)
                return ScopeValidationResult.Failure("invalid_scope", $"Scope '{requestedScope}' does not correspond to an active resource.");
        }

        return ScopeValidationResult.Success(string.Join(' ', requestedScopes));
    }

    #endregion

    #region 管理服务 (Workbench 使用)

    public async Task<List<ClientDto>> GetAllAsync(string? keyword = null, bool? isActive = null, CancellationToken ct = default)
    {
        var (clients, _) = await clientRepository.GetPagedAsync(keyword, isActive, 1, int.MaxValue, ct);
        return await MapClientsAsync(clients, ct);
    }

    public async Task<PagedResult<ClientDto>> GetPagedAsync(
        string? keyword = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var (clients, total) = await clientRepository.GetPagedAsync(keyword, isActive, normalizedPage, normalizedPageSize, ct);
        var items = await MapClientsAsync(clients, ct);
        return new PagedResult<ClientDto>(items, total, normalizedPage, normalizedPageSize);
    }

    public async Task<ClientDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var client = await clientRepository.GetByIdAsync(id, ct);
        return client is null ? null : await MapClientAsync(client, ct);
    }

    public async Task<ClientMutationResultDto> CreateAsync(CreateClientRequest request, CancellationToken ct = default)
    {
        var clientId = Guid.NewGuid();
        var apiResourceIds = NormalizeApiResourceIds(request.ApiResourceIds);
        var allowedScopes = await MergeAllowedScopesAsync(request.AllowedScopes, apiResourceIds, ct);
        var tokenEndpointAuthMethod = ResolveTokenEndpointAuthMethod(request.TokenEndpointAuthMethod);
        GeneratedCredential? generatedCredential = null;
        var requiresGeneratedJwks = string.Equals(tokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodPrivateKeyJwt, StringComparison.Ordinal)
            && request.AutoGenerateJwks == true;

        var initialJwks = requiresGeneratedJwks ? null : request.Jwks;

        var client = OAuthClient.Create(
            clientId,
            request.ClientId,
            request.ClientName,
            request.Description,
            request.RedirectUris,
            request.PostLogoutRedirectUris,
            allowedScopes,
            request.AllowedGrantTypes,
            request.RequirePkce,
            tokenEndpointAuthMethod,
            jwks: initialJwks,
            jwksUri: request.JwksUri);

        if (requiresGeneratedJwks
            || string.Equals(tokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodClientSecretBasic, StringComparison.Ordinal)
            || string.Equals(tokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodClientSecretPost, StringComparison.Ordinal)
            || string.Equals(tokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodClientSecretJwt, StringComparison.Ordinal))
        {
            generatedCredential = GenerateCredential(client.Id, tokenEndpointAuthMethod, request.AutoGenerateJwks, request.Description);

            if (!string.IsNullOrWhiteSpace(generatedCredential.Jwks))
                client.SetJwks(generatedCredential.Jwks);
        }

        await clientRepository.AddAsync(client, ct);

        if (generatedCredential?.Secret is not null)
            await clientSecretRepository.AddAsync(generatedCredential.Secret, ct);

        foreach (var apiResourceId in apiResourceIds)
        {
            var association = ClientApiResource.Create(client.Id, apiResourceId);
            await clientApiResourceRepository.AddAsync(association, ct);
        }

        return new ClientMutationResultDto(
            await MapClientAsync(client, ct),
            generatedCredential?.Dto);
    }

    public async Task<ClientDto> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default)
    {
        var client = await clientRepository.GetByIdAsync(id, ct) ?? throw new InvalidOperationException($"Client with id {id} not found.");
        var apiResourceIds = request.ApiResourceIds is null
            ? null
            : NormalizeApiResourceIds(request.ApiResourceIds);
        var allowedScopes = request.AllowedScopes;
        IReadOnlyList<ApiResource>? existingResources = null;
        if (apiResourceIds is not null || request.AllowedScopes is not null)
        {
            existingResources = await clientApiResourceRepository.GetResourcesByClientIdAsync(id, ct);
            var targetApiResourceIds = apiResourceIds ?? existingResources.Select(resource => resource.Id).ToList();
            var standardScopes = request.AllowedScopes
                ?? client.AllowedScopes.Except(existingResources.Select(resource => resource.Audience), StringComparer.Ordinal);

            allowedScopes = await MergeAllowedScopesAsync(standardScopes, targetApiResourceIds, ct);
        }

        var tokenEndpointAuthMethod = request.TokenEndpointAuthMethod is null
            ? client.TokenEndpointAuthMethod
            : ResolveTokenEndpointAuthMethod(request.TokenEndpointAuthMethod);

        client.Update(
            request.ClientName,
            request.Description,
            request.RedirectUris,
            request.PostLogoutRedirectUris,
            allowedScopes,
            request.AllowedGrantTypes,
            request.RequirePkce,
            request.IsActive,
            tokenEndpointAuthMethod,
            jwks: request.Jwks,
            jwksUri: request.JwksUri);

        await clientRepository.UpdateAsync(client, ct);

        if (apiResourceIds is not null)
        {
            var existingIds = (existingResources ?? []).Select(resource => resource.Id).ToHashSet();

            var toAdd = apiResourceIds.Except(existingIds);
            foreach (var apiResourceId in toAdd)
            {
                var association = ClientApiResource.Create(client.Id, apiResourceId);
                await clientApiResourceRepository.AddAsync(association, ct);
            }

            var toRemove = existingIds.Except(apiResourceIds);
            foreach (var apiResourceId in toRemove)
            {
                await clientApiResourceRepository.RemoveAsync(client.Id, apiResourceId, ct);
            }
        }

        return await MapClientAsync(client, ct);
    }

    public async Task<ClientMutationResultDto> GenerateCredentialAsync(Guid id, GenerateClientCredentialRequest request, CancellationToken ct = default)
    {
        var client = await clientRepository.GetByIdAsync(id, ct) ?? throw new InvalidOperationException($"Client with id {id} not found.");
        var tokenEndpointAuthMethod = ResolveCredentialAuthMethod(client, request.TokenEndpointAuthMethod);
        var generatedCredential = GenerateCredential(client.Id, tokenEndpointAuthMethod, request.AutoGenerateJwks, request.Description);
        if (generatedCredential.Secret is not null)
        {
            await clientSecretRepository.AddAsync(generatedCredential.Secret, ct);
        }
        else if (!string.IsNullOrWhiteSpace(generatedCredential.Jwks))
        {
            client.SetJwks(generatedCredential.Jwks);
            await clientRepository.UpdateAsync(client, ct);
        }

        client = await clientRepository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Client with id {id} not found.");

        return new ClientMutationResultDto(
            await MapClientAsync(client, ct),
            generatedCredential.Dto);
    }

    public async Task<ClientMutationResultDto> ResetCredentialAsync(Guid id, GenerateClientCredentialRequest request, CancellationToken ct = default)
    {
        var client = await clientRepository.GetByIdAsync(id, ct) ?? throw new InvalidOperationException($"Client with id {id} not found.");
        var tokenEndpointAuthMethod = ResolveCredentialAuthMethod(client, request.TokenEndpointAuthMethod);
        if (string.Equals(tokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodPrivateKeyJwt, StringComparison.Ordinal))
            throw new InvalidOperationException("private_key_jwt does not use shared secret reset. Update the registered JWKS instead.");

        foreach (var secret in client.ClientSecrets.Where(secret => secret.IsActive && string.Equals(secret.Type, OAuthClientSecret.TypeSharedSecret, StringComparison.Ordinal)))
        {
            secret.Disable();
        }

        var generatedCredential = GenerateCredential(client.Id, tokenEndpointAuthMethod, request.AutoGenerateJwks, request.Description);
        if (generatedCredential.Secret is null)
            throw new InvalidOperationException("Shared secret generation failed.");

        await clientSecretRepository.AddAsync(generatedCredential.Secret, ct);

        client = await clientRepository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Client with id {id} not found.");

        return new ClientMutationResultDto(
            await MapClientAsync(client, ct),
            generatedCredential.Dto);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var client = await clientRepository.GetByIdAsync(id, ct);
        if (client is not null)
            await clientRepository.DeleteAsync(client, ct);
    }

    #endregion

    private ClientAuthenticationResult AuthenticationSuccess(
        OAuthClient client,
        string outcome)
    {
        logger.LogLuckInformation(
            "Client authentication succeeded. ClientId={ClientId} Outcome={Outcome}",
            [client.ClientId, outcome]);

        return ClientAuthenticationResult.Success(client);
    }

    private ClientAuthenticationResult AuthenticationFailure(
        string? clientId,
        string reasonCode,
        string error)
    {
        logger.LogLuckWarning(
            "Client authentication failed. Reason={ReasonCode} Outcome={Outcome}",
            [reasonCode, reasonCode]);

        return ClientAuthenticationResult.Failure("invalid_client", error);
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

    private static string BuildClientAssertionReplayKey(string clientId, string assertionJti)
    {
        return $"client_assertion:{clientId}:{assertionJti}";
    }

    private static GeneratedCredential GenerateCredential(Guid clientId, string tokenEndpointAuthMethod, bool? autoGenerateJwks, string? description)
    {
        if (string.Equals(tokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodPrivateKeyJwt, StringComparison.Ordinal))
        {
            if (autoGenerateJwks is false)
                throw new InvalidOperationException("private_key_jwt credential generation only supports automatic JWKS generation.");

            var keyId = Guid.NewGuid().ToString("N");
            using var rsa = RSA.Create(2048);
            var parameters = rsa.ExportParameters(false);
            var privateKeyPem = ExportPrivateKeyPem(rsa);
            var jwks = JsonSerializer.Serialize(new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "RSA",
                        use = "sig",
                        alg = SecurityAlgorithms.RsaSha256,
                        kid = keyId,
                        n = Base64UrlEncoder.Encode(parameters.Modulus),
                        e = Base64UrlEncoder.Encode(parameters.Exponent),
                    }
                }
            });

            var dto = new GeneratedClientCredentialDto(
                "jwks",
                null,
                privateKeyPem,
                jwks,
                description);

            return new GeneratedCredential(null, dto, jwks);
        }

        if (string.Equals(tokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodClientSecretBasic, StringComparison.Ordinal)
            || string.Equals(tokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodClientSecretPost, StringComparison.Ordinal)
            || string.Equals(tokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodClientSecretJwt, StringComparison.Ordinal))
        {
            var secretValue = GenerateSecretValue();
            var secret = OAuthClientSecret.CreateSharedSecret(
                clientId,
                secretValue,
                description,
                persistPlainValue: string.Equals(tokenEndpointAuthMethod, OAuthClient.TokenEndpointAuthMethodClientSecretJwt, StringComparison.Ordinal));
            var dto = new GeneratedClientCredentialDto(
                OAuthClientSecret.TypeSharedSecret,
                secretValue,
                null,
                null,
                description);

            return new GeneratedCredential(secret, dto);
        }

        throw new InvalidOperationException($"Unsupported token_endpoint_auth_method '{tokenEndpointAuthMethod}'.");
    }

    private static string GenerateSecretValue()
    {
        return Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
    }

    private static string ExportPrivateKeyPem(RSA rsa)
    {
        var privateKeyBytes = rsa.ExportPkcs8PrivateKey();
        var base64 = Convert.ToBase64String(privateKeyBytes);
        var builder = new StringBuilder();
        builder.AppendLine("-----BEGIN PRIVATE KEY-----");

        for (var i = 0; i < base64.Length; i += 64)
        {
            builder.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }

        builder.Append("-----END PRIVATE KEY-----");
        return builder.ToString();
    }

    private static string ResolveTokenEndpointAuthMethod(string? tokenEndpointAuthMethod)
    {
        return string.IsNullOrWhiteSpace(tokenEndpointAuthMethod)
            ? OAuthClient.TokenEndpointAuthMethodClientSecretBasic
            : tokenEndpointAuthMethod.Trim();
    }

    private static string ResolveCredentialAuthMethod(OAuthClient client, string? requestedMethod)
    {
        var method = string.IsNullOrWhiteSpace(requestedMethod)
            ? client.TokenEndpointAuthMethod
            : requestedMethod.Trim();

        if (!client.AllowsTokenEndpointAuthMethod(method))
            throw new InvalidOperationException($"Client does not allow token_endpoint_auth_method '{method}'.");

        return method;
    }

    private static int NormalizePageSize(int pageSize)
    {
        return pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
    }

    private static List<Guid> NormalizeApiResourceIds(IEnumerable<Guid>? apiResourceIds)
    {
        return apiResourceIds?.Distinct().ToList() ?? [];
    }

    private async Task<List<string>> MergeAllowedScopesAsync(
        IEnumerable<string>? standardScopes,
        IReadOnlyCollection<Guid> apiResourceIds,
        CancellationToken ct)
    {
        var allowedScopes = standardScopes?
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .ToList() ?? [];

        var resources = await apiResourceRepository.FindByIdsAsync(apiResourceIds, ct);
        var foundResourceIds = resources.Select(resource => resource.Id).ToHashSet();
        var missingResourceIds = apiResourceIds.Where(apiResourceId => !foundResourceIds.Contains(apiResourceId)).ToList();
        if (missingResourceIds.Count > 0)
            throw new InvalidOperationException($"Api resource with id {missingResourceIds[0]} not found.");

        allowedScopes.AddRange(resources.Select(resource => resource.Audience));

        return [.. allowedScopes.Distinct(StringComparer.Ordinal)];
    }

    private async Task<List<ClientDto>> MapClientsAsync(IEnumerable<OAuthClient> clients, CancellationToken ct)
    {
        var clientList = clients.ToList();
        if (clientList.Count == 0)
        {
            return [];
        }

        var resourceIdsByClientId = await clientApiResourceRepository
            .GetApiResourceIdsByClientIdsAsync(clientList.Select(client => client.Id), ct);

        return [.. clientList.Select(client => MapClient(
            client,
            resourceIdsByClientId.GetValueOrDefault(client.Id, [])))];
    }

    private async Task<ClientDto> MapClientAsync(OAuthClient client, CancellationToken ct)
    {
        var resources = await clientApiResourceRepository.GetResourcesByClientIdAsync(client.Id, ct);
        return MapClient(client, resources.Select(resource => resource.Id));
    }

    private static ClientDto MapClient(OAuthClient client, IEnumerable<Guid> apiResourceIds)
    {
        return new ClientDto(
            client.Id,
            client.ClientId,
            [.. client.ClientSecrets.Select(MapCredential)],
            client.TokenEndpointAuthMethod,
            client.Jwks,
            client.JwksUri,
            client.ClientName,
            client.Description,
            client.RedirectUris,
            client.PostLogoutRedirectUris,
            client.AllowedScopes,
            client.AllowedGrantTypes,
            client.RequirePkce,
            client.IsActive,
            client.CreatedAt)
        {
            ApiResourceIds = [.. apiResourceIds],
        };
    }

    private static ClientCredentialDto MapCredential(OAuthClientSecret secret)
    {
        return new ClientCredentialDto(
            secret.Id,
            secret.Type,
            secret.IsActive,
            secret.CreatedAt);
    }

    private sealed record GeneratedCredential(
        OAuthClientSecret? Secret,
        GeneratedClientCredentialDto Dto,
        string? Jwks = null);
}
