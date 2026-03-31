using Luck.EntityFrameworkCore.DbContexts;
using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public class AuthorizationCodeRepository : EfCoreEntityRepository<AuthorizationCode, Guid>, IAuthorizationCodeRepository
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly LuckDbContextBase _dbContext;

    public AuthorizationCodeRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
                     ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");
    }

    public async Task<AuthorizationCode?> FindByCodeAsync(string code, CancellationToken ct = default)
    {
        return await FindAll(a => a.Code == code).FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(AuthorizationCode code, CancellationToken ct = default)
    {
        await _dbContext.Set<AuthorizationCode>().AddAsync(code, ct);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task MarkUsedAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await FindAsync(id);
        if (entity is not null)
        {
            entity.MarkAsUsed();
            await _unitOfWork.CommitAsync(ct);
        }
    }
}
