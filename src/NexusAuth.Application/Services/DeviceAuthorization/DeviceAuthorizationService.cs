
using NexusAuth.Application.Clients;
using NexusAuth.Application.Services.Security;

namespace NexusAuth.Application.Services.DeviceAuthorization;

public class DeviceAuthorizationService(
    IClientService clientService,
    IDeviceAuthorizationRepository deviceAuthorizationRepository,
    ISecurityPolicyService securityPolicyService) : IDeviceAuthorizationService
{
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

        var authorization = Domain.Entities.DeviceAuthorization.Create(clientId, scopeValidation.NormalizedScope!);
        await deviceAuthorizationRepository.AddAsync(authorization, ct);

        const string verificationUri = "/device";
        var verificationUriComplete = $"{verificationUri}?user_code={Uri.EscapeDataString(authorization.UserCode)}";

        return DeviceAuthorizationStartResult.Success(
            authorization.DeviceCode,
            authorization.UserCode,
            (int)(authorization.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds,
            5,
            verificationUri,
            verificationUriComplete);
    }

    public async Task<DeviceAuthorizationSessionResult> ApproveAsync(string userCode, Guid userId, CancellationToken ct = default)
    {
        var authorization = await FindByUserCodeAsync(userCode, ct);
        if (authorization is null)
            return DeviceAuthorizationSessionResult.Failure("Invalid or expired user code.");

        authorization.Approve(userId);
        await deviceAuthorizationRepository.UpdateAsync(authorization, ct);
        return DeviceAuthorizationSessionResult.Success(authorization.UserCode, authorization.ClientId, authorization.Scope, false, true, false);
    }

    public async Task<DeviceAuthorizationPollResult> PollAsync(ClientAuthenticationInput authentication, string deviceCode, CancellationToken ct = default)
    {
        var authorization = await deviceAuthorizationRepository.FindByDeviceCodeAsync(deviceCode, ct);
        if (authorization is null)
            return DeviceAuthorizationPollResult.Pending();

        if (authorization.Status == Domain.Entities.DeviceAuthorizationStatus.Pending)
            return DeviceAuthorizationPollResult.Pending();

        if (authorization.Status == Domain.Entities.DeviceAuthorizationStatus.Approved)
            return await ConsumeApprovedAsync(authorization, ct);

        return DeviceAuthorizationPollResult.Failure("authorization_denied", "Authorization denied.");
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

        authorization.Deny();
        await deviceAuthorizationRepository.UpdateAsync(authorization, ct);
        return MapSession(authorization);
    }

    private async Task<DeviceAuthorizationPollResult> ConsumeApprovedAsync(Domain.Entities.DeviceAuthorization authorization, CancellationToken ct)
    {
        authorization.MarkAsConsumed();
        await deviceAuthorizationRepository.UpdateAsync(authorization, ct);
        return DeviceAuthorizationPollResult.Success(authorization.UserId!.Value, authorization.ClientId, authorization.Scope);
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