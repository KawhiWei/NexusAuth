using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Application.Services.Sessions;

public sealed class SsoSessionService(ISsoSessionRepository sessionRepository) : ISsoSessionService
{
    private static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(24);

    public async Task<Guid> CreateAsync(Guid userId, CancellationToken ct = default)
    {
        var session = SsoSession.Create(userId, AbsoluteLifetime);
        await sessionRepository.AddAsync(session, ct);
        return session.Id;
    }

    public async Task<bool> IsActiveAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        return await sessionRepository.FindActiveAsync(sessionId, userId, DateTimeOffset.UtcNow, ct) is not null;
    }

    public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return sessionRepository.RevokeAllForUserAsync(userId, DateTimeOffset.UtcNow, ct);
    }
}
