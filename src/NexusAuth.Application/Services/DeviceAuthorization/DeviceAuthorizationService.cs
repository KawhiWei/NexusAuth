
using NexusAuth.Application.Clients;
using NexusAuth.Application.Services.Security;
using NexusAuth.Application.Services.Tokens;

namespace NexusAuth.Application.Services.DeviceAuthorization;

public class DeviceAuthorizationService(
    IClientService clientService,
    IDeviceAuthorizationRepository deviceAuthorizationRepository,
    ISecurityPolicyService securityPolicyService,
    IOptions<JwtOptions> jwtOptions) : IDeviceAuthorizationService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<DeviceAuthorizationStartResult> StartAsync(ClientAuthenticationInput authentication, string scope, CancellationToken ct = default)
    {
        var clientAuthentication = await clientService.AuthenticateClientAsync(authentication, requireClientAuthentication: true, ct);
        if (!clientAuthentication.IsSuccess)
            return DeviceAuthorizationStartResult.Failure(clientAuthentication.ErrorCode ?? "invalid_client", clientAuthentication.Error ?? "Invalid client.");

        var clientId = clientAuthentication.Client!.ClientId;

        var policy = securityPolicyService.CheckClient(clientId);
        if (!policy.IsSuccess)
            return DeviceAuthorizationStartResult.Failure("access_denied", policy.Error ?? "Client denied.");

        if (!clientAuthentication.Client!.IsGrantTypeAllowed("urn:ietf:params:oauth:grant-type:device_code"))
            return DeviceAuthorizationStartResult.Failure("unauthorized_client", "Client is not allowed to use device_code grant type.");

        var scopeValidation = await clientService.ValidateScopesAsync(clientId, scope, allowIdentityScopes: true, ct);
        if (!scopeValidation.IsSuccess)
            return DeviceAuthorizationStartResult.Failure(scopeValidation.ErrorCode ?? "invalid_scope", scopeValidation.Error ?? "Invalid scope.");

        var creation = Domain.Entities.DeviceAuthorization.Create(
            clientId,
            scopeValidation.NormalizedScope!,
            TimeSpan.FromMinutes(Math.Max(1, _jwtOptions.DeviceCodeLifetimeMinutes)));
        await deviceAuthorizationRepository.AddAsync(creation.Entity, ct);

        const string verificationUri = "/device";
        var verificationUriComplete = $"{verificationUri}?user_code={Uri.EscapeDataString(creation.Entity.UserCode)}";

        return DeviceAuthorizationStartResult.Success(
            creation.RawDeviceCode,
            creation.Entity.UserCode,
            Math.Max(1, (int)(creation.Entity.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds),
            creation.Entity.PollingIntervalSeconds,
            verificationUri,
            verificationUriComplete);
    }

    public async Task<DeviceAuthorizationSessionResult> ApproveAsync(string userCode, Guid userId, CancellationToken ct = default)
    {
        var authorization = await FindByUserCodeAsync(userCode, ct);
        if (authorization is null)
            return DeviceAuthorizationSessionResult.Failure("Invalid or expired user code.");

        var approved = await deviceAuthorizationRepository.ApprovePendingAsync(
            authorization.Id,
            userId,
            DateTimeOffset.UtcNow,
            ct);
        if (!approved)
            return DeviceAuthorizationSessionResult.Failure("Device authorization is no longer pending.");

        return DeviceAuthorizationSessionResult.Success(authorization.UserCode, authorization.ClientId, authorization.Scope, false, true, false);
    }

    public async Task<DeviceAuthorizationPollResult> PollAsync(ClientAuthenticationInput authentication, string deviceCode, CancellationToken ct = default)
    {
        var clientAuthentication = await clientService.AuthenticateClientAsync(authentication, requireClientAuthentication: true, ct);
        if (!clientAuthentication.IsSuccess)
            return DeviceAuthorizationPollResult.Failure(
                clientAuthentication.ErrorCode ?? "invalid_client",
                clientAuthentication.Error ?? "Invalid client.");

        if (string.IsNullOrWhiteSpace(deviceCode))
            return DeviceAuthorizationPollResult.Failure("invalid_request", "device_code is required.");

        var clientId = clientAuthentication.Client!.ClientId;
        var policy = securityPolicyService.CheckClient(clientId);
        if (!policy.IsSuccess)
            return DeviceAuthorizationPollResult.Failure("access_denied", policy.Error ?? "Client denied.");

        if (!clientAuthentication.Client.IsGrantTypeAllowed("urn:ietf:params:oauth:grant-type:device_code"))
            return DeviceAuthorizationPollResult.Failure("unauthorized_client", "Client is not allowed to use device_code grant type.");

        var deviceCodeHash = Domain.Entities.DeviceAuthorization.Hash(deviceCode);
        var authorization = await deviceAuthorizationRepository.FindByDeviceCodeHashAsync(deviceCodeHash, ct);
        if (authorization is null)
            return DeviceAuthorizationPollResult.Failure("invalid_grant", "Invalid device code.");

        // The device code is bearer material, but it must also remain bound to
        // the client that started the authorization request.
        if (!string.Equals(authorization.ClientId, clientId, StringComparison.Ordinal))
            return DeviceAuthorizationPollResult.Failure("invalid_grant", "Invalid device code.");

        var now = DateTimeOffset.UtcNow;
        if (authorization.ExpiresAt <= now)
            return DeviceAuthorizationPollResult.Failure("expired_token", "The device code has expired.");

        if (authorization.Status == Domain.Entities.DeviceAuthorizationStatus.Pending)
            return await RegisterPendingPollAsync(deviceCodeHash, authorization, now, ct);

        if (authorization.Status == Domain.Entities.DeviceAuthorizationStatus.Approved)
        {
            var consumed = await deviceAuthorizationRepository.ConsumeApprovedAsync(deviceCodeHash, clientId, now, ct);
            if (consumed is null || !consumed.UserId.HasValue)
                return DeviceAuthorizationPollResult.Failure("invalid_grant", "The device code has already been used.");

            return DeviceAuthorizationPollResult.Success(consumed.UserId.Value, consumed.ClientId, consumed.Scope);
        }

        if (authorization.Status == Domain.Entities.DeviceAuthorizationStatus.Denied)
            return DeviceAuthorizationPollResult.Failure("access_denied", "Authorization denied.");

        return DeviceAuthorizationPollResult.Failure("invalid_grant", "The device code has already been used.");
    }

    public async Task<DeviceAuthorizationSessionResult> GetByUserCodeAsync(string userCode, CancellationToken ct = default)
    {
        var authorization = await FindByUserCodeAsync(userCode, ct);
        if (authorization is null)
            return DeviceAuthorizationSessionResult.Failure("Invalid or expired user code.");

        return MapSession(authorization);
    }

    public async Task<DeviceAuthorizationSessionResult> DenyAsync(string userCode, CancellationToken ct = default)
    {
        var authorization = await FindByUserCodeAsync(userCode, ct);
        if (authorization is null)
            return DeviceAuthorizationSessionResult.Failure("Invalid or expired user code.");

        var denied = await deviceAuthorizationRepository.DenyPendingAsync(
            authorization.Id,
            DateTimeOffset.UtcNow,
            ct);
        if (!denied)
            return DeviceAuthorizationSessionResult.Failure("Device authorization is no longer pending.");

        return DeviceAuthorizationSessionResult.Success(
            authorization.UserCode,
            authorization.ClientId,
            authorization.Scope,
            false,
            false,
            true);
    }

    private async Task<DeviceAuthorizationPollResult> RegisterPendingPollAsync(
        string deviceCodeHash,
        Domain.Entities.DeviceAuthorization authorization,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // A failed compare-and-update means another poller changed the row.
        // Re-read once so concurrent callers observe the latest backoff.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var slowDown = authorization.RequiresSlowDown(now);
            var interval = authorization.PollingIntervalSeconds + (slowDown ? 5 : 0);
            if (await deviceAuthorizationRepository.TryRegisterPollAsync(
                authorization.Id,
                authorization.LastPolledAt,
                now,
                interval,
                ct))
            {
                return slowDown
                    ? DeviceAuthorizationPollResult.Failure("slow_down", "The client is polling too frequently.", interval)
                    : DeviceAuthorizationPollResult.Pending(interval);
            }

            var refreshedAuthorization = await deviceAuthorizationRepository.FindByDeviceCodeHashAsync(deviceCodeHash, ct);
            if (refreshedAuthorization is null)
                return DeviceAuthorizationPollResult.Failure("invalid_grant", "Invalid device code.");

            authorization = refreshedAuthorization;

            if (authorization.ExpiresAt <= DateTimeOffset.UtcNow)
                return DeviceAuthorizationPollResult.Failure("expired_token", "The device code has expired.");

            if (authorization.Status != Domain.Entities.DeviceAuthorizationStatus.Pending)
            {
                if (authorization.Status == Domain.Entities.DeviceAuthorizationStatus.Approved)
                {
                    var consumed = await deviceAuthorizationRepository.ConsumeApprovedAsync(
                        deviceCodeHash,
                        authorization.ClientId,
                        DateTimeOffset.UtcNow,
                        ct);
                    return consumed?.UserId is { } userId
                        ? DeviceAuthorizationPollResult.Success(userId, consumed.ClientId, consumed.Scope)
                        : DeviceAuthorizationPollResult.Failure("invalid_grant", "The device code has already been used.");
                }

                return authorization.Status == Domain.Entities.DeviceAuthorizationStatus.Denied
                    ? DeviceAuthorizationPollResult.Failure("access_denied", "Authorization denied.")
                    : DeviceAuthorizationPollResult.Failure("invalid_grant", "The device code has already been used.");
            }

            now = DateTimeOffset.UtcNow;
        }

        return DeviceAuthorizationPollResult.Failure("slow_down", "The client is polling too frequently.", authorization.PollingIntervalSeconds);
    }

    private async Task<Domain.Entities.DeviceAuthorization?> FindByUserCodeAsync(string userCode, CancellationToken ct)
    {
        var normalized = Domain.Entities.DeviceAuthorization.NormalizeUserCode(userCode);
        var authorization = await deviceAuthorizationRepository.FindByUserCodeAsync(normalized, ct);
        return authorization is null || authorization.ExpiresAt <= DateTimeOffset.UtcNow ? null : authorization;
    }

    private static DeviceAuthorizationSessionResult MapSession(Domain.Entities.DeviceAuthorization authorization)
    {
        var isPending = authorization.Status == Domain.Entities.DeviceAuthorizationStatus.Pending;
        var isApproved = authorization.Status == Domain.Entities.DeviceAuthorizationStatus.Approved;
        var isDenied = authorization.Status == Domain.Entities.DeviceAuthorizationStatus.Denied;

        return DeviceAuthorizationSessionResult.Success(
            authorization.UserCode,
            authorization.ClientId,
            authorization.Scope,
            isPending,
            isApproved,
            isDenied);
    }
}
