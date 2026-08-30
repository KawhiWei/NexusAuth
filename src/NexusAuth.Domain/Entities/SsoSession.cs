using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

/// <summary>
/// Server-side authority for a browser SSO session. The cookie only carries this identifier.
/// </summary>
public sealed class SsoSession : EntityWithIdentity<Guid>
{
    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    private SsoSession(Guid id) : base(id)
    {
    }

    public static SsoSession Create(Guid userId, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lifetime));

        var now = DateTimeOffset.UtcNow;
        return new SsoSession(Guid.NewGuid())
        {
            UserId = userId,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        };
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now)
    {
        RevokedAt ??= now;
    }
}
