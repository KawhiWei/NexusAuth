using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

public class TokenBlacklistEntry : EntityWithIdentity<Guid>
{
    public string Jti { get; private set; } = default!;

    public string TokenType { get; private set; } = default!;

    public string? Subject { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset RevokedAt { get; private set; }

    private TokenBlacklistEntry(Guid id) : base(id)
    {
    }

    public static TokenBlacklistEntry Create(string jti, string tokenType, string? subject, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenType);

        return new TokenBlacklistEntry(Guid.NewGuid())
        {
            Jti = jti,
            TokenType = tokenType,
            Subject = subject,
            ExpiresAt = expiresAt,
            RevokedAt = DateTimeOffset.UtcNow,
        };
    }
}
