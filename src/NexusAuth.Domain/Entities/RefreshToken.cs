using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

public class RefreshToken : EntityWithIdentity<Guid>
{
    /// <summary>
    /// SHA-256 hash of the bearer token, encoded as unpadded Base64Url.
    /// The raw token is deliberately not part of the entity and must only be
    /// carried by <see cref="RefreshTokenCreationResult"/> until returned to
    /// the client.
    /// </summary>
    public string TokenHash { get; private set; } = default!;

    public string ClientId { get; private set; } = default!;

    public Guid UserId { get; private set; }

    public string Scope { get; private set; } = default!;

    public bool IsRevoked { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// EF Core constructor
    /// </summary>
    private RefreshToken(Guid id) : base(id)
    {
    }

    public static RefreshTokenCreationResult Create(
        string clientId,
        Guid userId,
        string scope,
        TimeSpan? lifetime = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var now = DateTimeOffset.UtcNow;

        var rawToken = GenerateUrlSafeRandomString(64);
        var entity = new RefreshToken(Guid.NewGuid())
        {
            TokenHash = Hash(rawToken),
            ClientId = clientId,
            UserId = userId,
            Scope = scope,
            IsRevoked = false,
            ExpiresAt = now.Add(lifetime ?? TimeSpan.FromDays(30)),
            CreatedAt = now,
        };

        return new RefreshTokenCreationResult(entity, rawToken);
    }

    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public void Revoke()
    {
        IsRevoked = true;
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

public sealed record RefreshTokenCreationResult(RefreshToken Entity, string RawToken);
