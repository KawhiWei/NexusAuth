using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Application.Services;

public class DeviceAuthorizationService : IDeviceAuthorizationService
{
    private readonly IClientService _clientService;
    private readonly IDeviceAuthorizationRepository _deviceAuthorizationRepository;
    private readonly ISecurityPolicyService _securityPolicyService;

    public DeviceAuthorizationService(
        IClientService clientService,
        IDeviceAuthorizationRepository deviceAuthorizationRepository,
        ISecurityPolicyService securityPolicyService)
    {
        _clientService = clientService;
        _deviceAuthorizationRepository = deviceAuthorizationRepository;
        _securityPolicyService = securityPolicyService;
    }

    /// <summary>
    /// 发起 device authorization 请求，生成 device_code / user_code。
    /// 主要调用方：OpenIdController 的 /connect/deviceauthorization。
    /// </summary>
    public async Task<DeviceAuthorizationStartResult> StartAsync(ClientAuthenticationInput authentication, string scope, CancellationToken ct = default)
    {
        // 中文注释：设备授权常用于输入受限设备，应允许 public client 使用，
        // 因此这里不再强制要求 client_secret。
        var clientAuthentication = await _clientService.AuthenticateClientAsync(authentication, requireClientAuthentication: true, ct);
        if (!clientAuthentication.IsSuccess)
            return DeviceAuthorizationStartResult.Failure(clientAuthentication.ErrorCode ?? "invalid_client", clientAuthentication.Error ?? "Invalid client.");

        var clientId = clientAuthentication.Client!.ClientId;

        var policy = _securityPolicyService.CheckClient(clientId);
        if (!policy.IsSuccess)
            return DeviceAuthorizationStartResult.Failure("access_denied", policy.Error ?? "Client denied.");

        if (!clientAuthentication.Client!.IsGrantTypeAllowed("urn:ietf:params:oauth:grant-type:device_code"))
            return DeviceAuthorizationStartResult.Failure("unauthorized_client", "Client is not allowed to use device_code grant type.");

        var scopeValidation = await _clientService.ValidateScopesAsync(clientId, scope, allowIdentityScopes: true, ct);
        if (!scopeValidation.IsSuccess)
            return DeviceAuthorizationStartResult.Failure(scopeValidation.ErrorCode ?? "invalid_scope", scopeValidation.Error ?? "Invalid scope.");

        var authorization = DeviceAuthorization.Create(clientId, scopeValidation.NormalizedScope!);
        await _deviceAuthorizationRepository.AddAsync(authorization, ct);

        const string verificationUri = "/device";
        var verificationUriComplete = $"{verificationUri}?user_code={Uri.EscapeDataString(authorization.UserCode)}";

        return DeviceAuthorizationStartResult.Success(
            authorization.DeviceCode,
            authorization.UserCode,
            (int)(authorization.ExpiresAt - authorization.CreatedAt).TotalSeconds,
            authorization.PollingIntervalSeconds,
            verificationUri,
            verificationUriComplete);
    }

    /// <summary>
    /// 轮询 device_code 状态，完成后返回用户信息用于签发 token。
    /// 主要调用方：TokenController 的 device_code 分支。
    /// </summary>
    public async Task<DeviceAuthorizationPollResult> PollAsync(ClientAuthenticationInput authentication, string deviceCode, CancellationToken ct = default)
    {
        var clientAuthentication = await _clientService.AuthenticateClientAsync(authentication, requireClientAuthentication: true, ct);
        if (!clientAuthentication.IsSuccess)
            return DeviceAuthorizationPollResult.Failure(clientAuthentication.ErrorCode ?? "invalid_client", clientAuthentication.Error ?? "Invalid client.");

        var clientId = clientAuthentication.Client!.ClientId;

        var authorization = await _deviceAuthorizationRepository.FindByDeviceCodeAsync(deviceCode, ct);
        if (authorization is null || !string.Equals(authorization.ClientId, clientId, StringComparison.Ordinal))
            return DeviceAuthorizationPollResult.Failure("invalid_grant", "Invalid device_code.");

        if (authorization.ExpiresAt <= DateTimeOffset.UtcNow)
            return DeviceAuthorizationPollResult.Failure("expired_token", "The device_code has expired.");

        var slowDown = authorization.RequiresSlowDown(DateTimeOffset.UtcNow);
        authorization.RegisterPoll(DateTimeOffset.UtcNow, slowDown);
        await _deviceAuthorizationRepository.UpdateAsync(authorization, ct);

        if (slowDown)
            return DeviceAuthorizationPollResult.Failure("slow_down", "Polling too frequently.", authorization.PollingIntervalSeconds);

        return authorization.Status switch
        {
            DeviceAuthorizationStatus.Pending => DeviceAuthorizationPollResult.Failure("authorization_pending", "Authorization is still pending.", authorization.PollingIntervalSeconds),
            DeviceAuthorizationStatus.Denied => DeviceAuthorizationPollResult.Failure("access_denied", "The device authorization request was denied."),
            DeviceAuthorizationStatus.Approved when authorization.UserId.HasValue => await ConsumeApprovedAsync(authorization, ct),
            DeviceAuthorizationStatus.Consumed => DeviceAuthorizationPollResult.Failure("invalid_grant", "The device_code has already been consumed."),
            _ => DeviceAuthorizationPollResult.Failure("invalid_grant", "The device authorization request is invalid."),
        };
    }

    /// <summary>
    /// 按 user_code 查询设备授权会话。
    /// </summary>
    public async Task<DeviceAuthorizationSessionResult> GetByUserCodeAsync(string userCode, CancellationToken ct = default)
    {
        var authorization = await FindByUserCodeAsync(userCode, ct);
        if (authorization is null)
            return DeviceAuthorizationSessionResult.Failure("Invalid or expired user code.");

        return MapSession(authorization);
    }

    /// <summary>
    /// 用户确认设备授权。
    /// </summary>
    public async Task<DeviceAuthorizationSessionResult> ApproveAsync(string userCode, Guid userId, CancellationToken ct = default)
    {
        var authorization = await FindByUserCodeAsync(userCode, ct);
        if (authorization is null)
            return DeviceAuthorizationSessionResult.Failure("Invalid or expired user code.");

        authorization.Approve(userId);
        await _deviceAuthorizationRepository.UpdateAsync(authorization, ct);
        return MapSession(authorization);
    }

    /// <summary>
    /// 用户拒绝设备授权。
    /// </summary>
    public async Task<DeviceAuthorizationSessionResult> DenyAsync(string userCode, CancellationToken ct = default)
    {
        var authorization = await FindByUserCodeAsync(userCode, ct);
        if (authorization is null)
            return DeviceAuthorizationSessionResult.Failure("Invalid or expired user code.");

        authorization.Deny();
        await _deviceAuthorizationRepository.UpdateAsync(authorization, ct);
        return MapSession(authorization);
    }

    private async Task<DeviceAuthorizationPollResult> ConsumeApprovedAsync(DeviceAuthorization authorization, CancellationToken ct)
    {
        authorization.MarkAsConsumed();
        await _deviceAuthorizationRepository.UpdateAsync(authorization, ct);
        return DeviceAuthorizationPollResult.Success(authorization.UserId!.Value, authorization.ClientId, authorization.Scope);
    }

    private async Task<DeviceAuthorization?> FindByUserCodeAsync(string userCode, CancellationToken ct)
    {
        var normalized = DeviceAuthorization.NormalizeUserCode(userCode);
        var authorization = await _deviceAuthorizationRepository.FindByUserCodeAsync(normalized, ct);
        if (authorization is null || authorization.ExpiresAt <= DateTimeOffset.UtcNow)
            return null;

        return authorization;
    }

    private static DeviceAuthorizationSessionResult MapSession(DeviceAuthorization authorization)
    {
        return DeviceAuthorizationSessionResult.Success(
            authorization.UserCode,
            authorization.ClientId,
            authorization.Scope,
            authorization.Status == DeviceAuthorizationStatus.Pending,
            authorization.Status == DeviceAuthorizationStatus.Approved || authorization.Status == DeviceAuthorizationStatus.Consumed,
            authorization.Status == DeviceAuthorizationStatus.Denied);
    }
}
