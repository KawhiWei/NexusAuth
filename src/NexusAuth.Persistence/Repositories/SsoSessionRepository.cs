using Luck.EntityFrameworkCore.DbContexts;
using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public sealed class SsoSessionRepository(IUnitOfWork unitOfWork)
    : EfCoreEntityRepository<SsoSession, Guid>(unitOfWork), ISsoSessionRepository
{
    private readonly LuckDbContextBase dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
        ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");
    private readonly IUnitOfWork unitOfWork = unitOfWork;

    public async Task AddAsync(SsoSession session, CancellationToken ct = default)
    {
        dbContext.Add(session);
        await unitOfWork.CommitAsync(ct);
    }

    public Task<SsoSession?> FindActiveAsync(Guid sessionId, Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        return FindAll(session => session.Id == sessionId
                && session.UserId == userId
                && session.RevokedAt == null
                && session.ExpiresAt > now)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        await dbContext.Set<SsoSession>()
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, now), ct);
    }
}
