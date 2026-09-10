using System.Security.Cryptography;
using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

/// <summary>
/// WebAuthn 注册或认证过程中的短期一次性状态。数据库只保存 bearer token 的 SHA-256 哈希，
/// 即使 challenge 表泄露，也不能直接完成注册或登录流程。
/// </summary>
public sealed class WebAuthnChallenge : EntityWithIdentity<Guid>
{
    public const string RegistrationPurpose = "registration";
    public const string AuthenticationPurpose = "authentication";

    public string TokenHash { get; private set; } = default!;
    public string Purpose { get; private set; } = default!;
    public Guid? UserId { get; private set; }
    public string OptionsJson { get; private set; } = default!;
    public string? ReturnUrl { get; private set; }
    public bool RememberMe { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private WebAuthnChallenge(Guid id) : base(id) { }

    public static WebAuthnChallenge Create(
        string token,
        string purpose,
        Guid? userId,
        string optionsJson,
        string? returnUrl,
        bool rememberMe,
        DateTimeOffset expiresAt,
        DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(optionsJson);
        if (purpose is not RegistrationPurpose and not AuthenticationPurpose)
            throw new ArgumentOutOfRangeException(nameof(purpose));

        var timestamp = now ?? DateTimeOffset.UtcNow;
        if (expiresAt <= timestamp)
            throw new ArgumentOutOfRangeException(nameof(expiresAt));

        return new WebAuthnChallenge(Guid.NewGuid())
        {
            TokenHash = HashToken(token),
            Purpose = purpose,
            UserId = userId,
            OptionsJson = optionsJson,
            ReturnUrl = returnUrl,
            RememberMe = rememberMe,
            ExpiresAt = expiresAt,
            CreatedAt = timestamp,
        };
    }

    /// <summary>
    /// 尝试消费当前挑战。过期或已经消费的挑战不能再次进入验证流程。
    /// </summary>
    public bool TryConsume(DateTimeOffset now)
    {
        if (ConsumedAt.HasValue || ExpiresAt <= now)
            return false;

        ConsumedAt = now;
        return true;
    }

    /// <summary>
    /// 将无用户名认证阶段解析出的可信用户绑定到已消费的登录挑战。
    /// </summary>
    public void AssignAuthenticatedUser(Guid userId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);

        if (Purpose != AuthenticationPurpose)
            throw new InvalidOperationException("Only authentication challenges can assign a user after creation.");
        if (!ConsumedAt.HasValue)
            throw new InvalidOperationException("The authentication challenge must be consumed before assigning a user.");
        if (UserId.HasValue && UserId.Value != userId)
            throw new InvalidOperationException("The authentication challenge is already assigned to another user.");

        UserId = userId;
    }

    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
