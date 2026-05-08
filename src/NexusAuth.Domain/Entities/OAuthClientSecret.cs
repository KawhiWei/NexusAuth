using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

public class OAuthClientSecret : EntityWithIdentity<Guid>
{
    public const string TypeSharedSecret = "shared_secret";

    public Guid ClientId { get; private set; }

    public string Type { get; private set; } = default!;

    public string Value { get; private set; } = default!;

    public string? PlainValue { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private OAuthClientSecret(Guid id) : base(id)
    {
    }

    public static OAuthClientSecret CreateSharedSecret(Guid clientId, string rawSecret, string? description = null, bool persistPlainValue = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawSecret);

        return new OAuthClientSecret(Guid.NewGuid())
        {
            ClientId = clientId,
            Type = TypeSharedSecret,
            Value = BCrypt.Net.BCrypt.HashPassword(rawSecret, workFactor: 12),
            PlainValue = persistPlainValue ? rawSecret : null,
            Description = description,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public bool VerifySharedSecret(string rawSecret)
    {
        if (!IsActive || !string.Equals(Type, TypeSharedSecret, StringComparison.Ordinal))
            return false;

        return BCrypt.Net.BCrypt.Verify(rawSecret, Value);
    }

    public void Disable()
    {
        IsActive = false;
    }
}
