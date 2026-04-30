using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

public class OAuthClientSecret : EntityWithIdentity<Guid>
{
    public const string TypeSharedSecret = "shared_secret";

    public const string TypeJwks = "jwks";

    public Guid ClientId { get; private set; }

    public string Type { get; private set; } = default!;

    public string Value { get; private set; } = default!;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? KeyId { get; private set; }

    private OAuthClientSecret(Guid id) : base(id)
    {
    }

    public static OAuthClientSecret CreateSharedSecret(Guid clientId, string rawSecret, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawSecret);

        return new OAuthClientSecret(Guid.NewGuid())
        {
            ClientId = clientId,
            Type = TypeSharedSecret,
            Value = BCrypt.Net.BCrypt.HashPassword(rawSecret, workFactor: 12),
            Description = description,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static OAuthClientSecret CreateJwks(Guid clientId, string jwksJson, string? description = null, string? keyId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jwksJson);

        return new OAuthClientSecret(Guid.NewGuid())
        {
            ClientId = clientId,
            Type = TypeJwks,
            Value = jwksJson,
            Description = description,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            KeyId = keyId,
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
