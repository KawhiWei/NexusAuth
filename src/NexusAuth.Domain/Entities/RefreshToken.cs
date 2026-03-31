using System.Security.Cryptography;
using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

public class RefreshToken : EntityWithIdentity<Guid>
{
    public string Token { get; private set; } = default!;

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

    public static RefreshToken Create(
        string clientId,
        Guid userId,
        string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var now = DateTimeOffset.UtcNow;

        return new RefreshToken(Guid.NewGuid())
        {
            Token = GenerateUrlSafeRandomString(64),
            ClientId = clientId,
            UserId = userId,
            Scope = scope,
            IsRevoked = false,
            ExpiresAt = now.AddDays(30),
            CreatedAt = now,
        };
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
