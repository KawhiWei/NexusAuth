using Luck.EntityFrameworkCore.DbContexts;
using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public class RefreshTokenRepository(IUnitOfWork unitOfWork) : EfCoreEntityRepository<RefreshToken, Guid>(unitOfWork), IRefreshTokenRepository
{
    private readonly LuckDbContextBase _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
        ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");

    public async Task<RefreshToken?> FindByTokenAsync(string token, CancellationToken ct = default)
    {
        return await FindAll(r => r.Token == token).FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        await _dbContext.Set<RefreshToken>().AddAsync(token, ct);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await FindAsync(id);
        if (entity is not null)
        {
            entity.Revoke();
            await unitOfWork.CommitAsync(ct);
        }
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await FindAll(r => r.UserId == userId && !r.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.Revoke();
        }

        await unitOfWork.CommitAsync();
    }
}
