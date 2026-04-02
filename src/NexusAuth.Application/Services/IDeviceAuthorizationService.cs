using Luck.Framework.Infrastructure.DependencyInjectionModule;

namespace NexusAuth.Application.Services;

public interface IDeviceAuthorizationService : IScopedDependency
{
    Task<DeviceAuthorizationStartResult> StartAsync(string clientId, string? clientSecret, string scope, CancellationToken ct = default);

    Task<DeviceAuthorizationPollResult> PollAsync(string clientId, string? clientSecret, string deviceCode, CancellationToken ct = default);

    Task<DeviceAuthorizationSessionResult> GetByUserCodeAsync(string userCode, CancellationToken ct = default);

    Task<DeviceAuthorizationSessionResult> ApproveAsync(string userCode, Guid userId, CancellationToken ct = default);

    Task<DeviceAuthorizationSessionResult> DenyAsync(string userCode, CancellationToken ct = default);
}

public record DeviceAuthorizationStartResult(
    bool IsSuccess,
    string? Error,
    string? ErrorCode,
    string? DeviceCode,
    string? UserCode,
    int ExpiresIn,
    int Interval,
    string? VerificationUri,
    string? VerificationUriComplete)
{
    public static DeviceAuthorizationStartResult Success(string deviceCode, string userCode, int expiresIn, int interval, string verificationUri, string verificationUriComplete)
        => new(true, null, null, deviceCode, userCode, expiresIn, interval, verificationUri, verificationUriComplete);

    public static DeviceAuthorizationStartResult Failure(string errorCode, string error)
        => new(false, error, errorCode, null, null, 0, 0, null, null);
}

public record DeviceAuthorizationPollResult(
    bool IsSuccess,
    string? Error,
    string? ErrorCode,
    Guid UserId,
    string? Scope,
    string? ClientId,
    int Interval)
{
    public static DeviceAuthorizationPollResult Success(Guid userId, string clientId, string scope)
        => new(true, null, null, userId, scope, clientId, 0);

    public static DeviceAuthorizationPollResult Failure(string errorCode, string error, int interval = 0)
        => new(false, error, errorCode, Guid.Empty, null, null, interval);
}

public record DeviceAuthorizationSessionResult(
    bool IsSuccess,
    string? Error,
    string? UserCode,
    string? ClientId,
    string? Scope,
    bool IsPending,
    bool IsApproved,
    bool IsDenied)
{
    public static DeviceAuthorizationSessionResult Success(string userCode, string clientId, string scope, bool isPending, bool isApproved, bool isDenied)
        => new(true, null, userCode, clientId, scope, isPending, isApproved, isDenied);

    public static DeviceAuthorizationSessionResult Failure(string error)
        => new(false, error, null, null, null, false, false, false);
}
