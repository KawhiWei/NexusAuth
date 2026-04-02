using System.Security.Cryptography;
using System.Text;
using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

public class DeviceAuthorization : EntityWithIdentity<Guid>
{
    private const string UserCodeAlphabet = "BCDFGHJKLMNPQRSTVWXZ";

    public string DeviceCode { get; private set; } = default!;

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

    public static DeviceAuthorization Create(string clientId, string scope, int intervalSeconds = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var now = DateTimeOffset.UtcNow;
        var userCode = GenerateUserCode();

        return new DeviceAuthorization(Guid.NewGuid())
        {
            DeviceCode = GenerateUrlSafeRandomString(48),
            UserCode = userCode,
            UserCodeNormalized = NormalizeUserCode(userCode),
            ClientId = clientId,
            Scope = scope,
            Status = DeviceAuthorizationStatus.Pending,
            PollingIntervalSeconds = intervalSeconds,
            ExpiresAt = now.AddMinutes(15),
            CreatedAt = now,
        };
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
