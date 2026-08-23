using System.Security.Cryptography;
using System.Text;
using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

public class DeviceAuthorization : EntityWithIdentity<Guid>
{
    private const string UserCodeAlphabet = "BCDFGHJKLMNPQRSTVWXZ";

    /// <summary>
    /// SHA-256 hash of the bearer device code. The raw code is returned once
    /// from the device authorization endpoint and is never persisted.
    /// </summary>
    public string DeviceCodeHash { get; private set; } = default!;

    public string UserCode { get; private set; } = default!;

    public string UserCodeNormalized { get; private set; } = default!;

    public string ClientId { get; private set; } = default!;

    public string Scope { get; private set; } = default!;

    public Guid? UserId { get; private set; }

    public DeviceAuthorizationStatus Status { get; private set; }

    public int PollingIntervalSeconds { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? AuthorizedAt { get; private set; }

    public DateTimeOffset? LastPolledAt { get; private set; }

    private DeviceAuthorization(Guid id) : base(id)
    {
    }

    public static DeviceAuthorizationCreationResult Create(
        string clientId,
        string scope,
        TimeSpan? lifetime = null,
        int intervalSeconds = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var now = DateTimeOffset.UtcNow;
        var userCode = GenerateUserCode();
        var rawDeviceCode = GenerateUrlSafeRandomString(48);

        var authorization = new DeviceAuthorization(Guid.NewGuid())
        {
            DeviceCodeHash = Hash(rawDeviceCode),
            UserCode = userCode,
            UserCodeNormalized = NormalizeUserCode(userCode),
            ClientId = clientId,
            Scope = scope,
            Status = DeviceAuthorizationStatus.Pending,
            PollingIntervalSeconds = intervalSeconds,
            ExpiresAt = now.Add(lifetime ?? TimeSpan.FromMinutes(15)),
            CreatedAt = now,
        };

        return new DeviceAuthorizationCreationResult(authorization, rawDeviceCode);
    }

    public static string Hash(string rawDeviceCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawDeviceCode);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(rawDeviceCode));
        return Convert.ToBase64String(digest)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public bool RequiresSlowDown(DateTimeOffset now)
    {
        return LastPolledAt.HasValue && now < LastPolledAt.Value.AddSeconds(PollingIntervalSeconds);
    }

    public void RegisterPoll(DateTimeOffset now, bool slowDown)
    {
        LastPolledAt = now;
        if (slowDown)
            PollingIntervalSeconds += 5;
    }

    public void Approve(Guid userId)
    {
        if (Status != DeviceAuthorizationStatus.Pending)
            return;

        UserId = userId;
        Status = DeviceAuthorizationStatus.Approved;
        AuthorizedAt = DateTimeOffset.UtcNow;
    }

    public void Deny()
    {
        if (Status != DeviceAuthorizationStatus.Pending)
            return;

        Status = DeviceAuthorizationStatus.Denied;
        AuthorizedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsConsumed()
    {
        Status = DeviceAuthorizationStatus.Consumed;
    }

    public static string NormalizeUserCode(string userCode)
    {
        if (string.IsNullOrWhiteSpace(userCode))
            return string.Empty;

        var builder = new StringBuilder(userCode.Length);
        foreach (var ch in userCode.ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
        }

        return builder.ToString();
    }

    private static string GenerateUserCode()
    {
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = UserCodeAlphabet[RandomNumberGenerator.GetInt32(UserCodeAlphabet.Length)];
        }

        return string.Create(9, chars, static (buffer, value) =>
        {
            buffer[0] = value[0];
            buffer[1] = value[1];
            buffer[2] = value[2];
            buffer[3] = value[3];
            buffer[4] = '-';
            buffer[5] = value[4];
            buffer[6] = value[5];
            buffer[7] = value[6];
            buffer[8] = value[7];
        });
    }

    private static string GenerateUrlSafeRandomString(int byteLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

public sealed record DeviceAuthorizationCreationResult(
    DeviceAuthorization Entity,
    string RawDeviceCode);
