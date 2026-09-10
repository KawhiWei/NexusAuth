using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

/// <summary>
/// 一个 WebAuthn 认证器的长期公钥凭据。私钥始终保留在认证器中，NexusAuth 不会保存私钥。
/// </summary>
public sealed class WebAuthnCredential : EntityWithIdentity<Guid>
{
    public Guid UserId { get; private set; }
    public byte[] CredentialId { get; private set; } = default!;
    public byte[] PublicKeyCose { get; private set; } = default!;
    public uint SignatureCounter { get; private set; }
    public Guid Aaguid { get; private set; }
    public string[] Transports { get; private set; } = [];
    public bool IsBackupEligible { get; private set; }
    public bool IsBackedUp { get; private set; }
    public string DisplayName { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }

    public bool IsEnabled => DisabledAt is null;

    private WebAuthnCredential(Guid id) : base(id) { }

    public static WebAuthnCredential Create(
        Guid userId,
        byte[] credentialId,
        byte[] publicKeyCose,
        uint signatureCounter,
        Guid aaguid,
        IEnumerable<string>? transports,
        bool isBackupEligible,
        bool isBackedUp,
        string? displayName = null,
        DateTimeOffset? now = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(credentialId);
        ArgumentNullException.ThrowIfNull(publicKeyCose);
        if (credentialId.Length == 0 || publicKeyCose.Length == 0)
            throw new ArgumentException("A WebAuthn credential and its public key are required.");

        var timestamp = now ?? DateTimeOffset.UtcNow;
        return new WebAuthnCredential(Guid.NewGuid())
        {
            UserId = userId,
            CredentialId = credentialId,
            PublicKeyCose = publicKeyCose,
            SignatureCounter = signatureCounter,
            Aaguid = aaguid,
            Transports = transports?.Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? [],
            IsBackupEligible = isBackupEligible,
            IsBackedUp = isBackedUp,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Passkey" : displayName.Trim(),
            CreatedAt = timestamp,
        };
    }

    public void RecordAuthentication(uint signatureCounter, bool isBackedUp, DateTimeOffset now)
    {
        // 不支持签名计数器的认证器允许返回 0；非零计数器必须单调递增，防止凭据克隆或回退。
        if (signatureCounter > 0 && SignatureCounter > 0 && signatureCounter <= SignatureCounter)
            throw new InvalidOperationException("The passkey signature counter did not advance.");

        if (signatureCounter > 0)
            SignatureCounter = signatureCounter;
        IsBackedUp = isBackedUp;
        LastUsedAt = now;
    }

    public void Disable(DateTimeOffset now)
    {
        DisabledAt = now;
    }
}
