using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

/// <summary>
/// A user-owned authentication credential. A user can register multiple
/// credentials of the same type, such as a primary and a backup TOTP device.
/// </summary>
public sealed class UserCredential : EntityWithIdentity<Guid>
{
    public const string TotpType = "totp";

    public Guid UserId { get; private set; }
    public string Type { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string? SecretProtected { get; private set; }
    public string? PendingSecretProtected { get; private set; }
    public DateTimeOffset? PendingExpiresAt { get; private set; }
    public bool IsEnabled { get; private set; }
    public long? LastUsedCounter { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }

    private UserCredential(Guid id) : base(id) { }

    public static UserCredential CreatePendingTotp(
        Guid userId,
        string protectedSecret,
        DateTimeOffset expiresAt,
        string? displayName = null,
        DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);
        var createdAt = now ?? DateTimeOffset.UtcNow;
        if (expiresAt <= createdAt)
            throw new ArgumentOutOfRangeException(nameof(expiresAt));

        return new UserCredential(Guid.NewGuid())
        {
            UserId = userId,
            Type = TotpType,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Authenticator" : displayName.Trim(),
            PendingSecretProtected = protectedSecret,
            PendingExpiresAt = expiresAt,
            IsEnabled = false,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
    }

    public bool ConfirmTotp(string expectedProtectedSecret, long counter, DateTimeOffset now)
    {
        if (counter < 0)
            throw new ArgumentOutOfRangeException(nameof(counter));

        if (!string.Equals(PendingSecretProtected, expectedProtectedSecret, StringComparison.Ordinal)
            || !PendingExpiresAt.HasValue || PendingExpiresAt <= now)
        {
            return false;
        }

        SecretProtected = PendingSecretProtected;
        PendingSecretProtected = null;
        PendingExpiresAt = null;
        IsEnabled = true;
        LastUsedCounter = counter;
        UpdatedAt = now;
        return true;
    }

    public bool TryUseTotpCounter(long counter, DateTimeOffset now)
    {
        if (counter < 0)
            throw new ArgumentOutOfRangeException(nameof(counter));

        if (!IsEnabled || string.IsNullOrWhiteSpace(SecretProtected) || LastUsedCounter >= counter)
            return false;

        LastUsedCounter = counter;
        UpdatedAt = now;
        return true;
    }

    public void Disable(DateTimeOffset now)
    {
        IsEnabled = false;
        SecretProtected = null;
        PendingSecretProtected = null;
        PendingExpiresAt = null;
        LastUsedCounter = null;
        DisabledAt = now;
        UpdatedAt = now;
    }
}
