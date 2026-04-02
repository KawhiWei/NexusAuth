using Luck.EntityFrameworkCore.DbContexts;
using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public class TokenBlacklistRepository : EfCoreEntityRepository<TokenBlacklistEntry, Guid>, ITokenBlacklistRepository
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly LuckDbContextBase _dbContext;

    public TokenBlacklistRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
                     ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");
    }

    public async Task<TokenBlacklistEntry?> FindByJtiAsync(string jti, CancellationToken ct = default)
    {
        return await FindAll(t => t.Jti == jti).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveAsync(string jti, DateTimeOffset now, CancellationToken ct = default)
    {
        return await FindAll(t => t.Jti == jti && t.ExpiresAt > now).AnyAsync(ct);
    }

    public async Task AddAsync(TokenBlacklistEntry entry, CancellationToken ct = default)
    {
        await _dbContext.Set<TokenBlacklistEntry>().AddAsync(entry, ct);
        await _unitOfWork.CommitAsync(ct);
    }
}
