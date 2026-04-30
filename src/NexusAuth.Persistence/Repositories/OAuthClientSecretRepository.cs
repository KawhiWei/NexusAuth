using Luck.EntityFrameworkCore.DbContexts;
using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public class OAuthClientSecretRepository(IUnitOfWork unitOfWork)
    : EfCoreEntityRepository<OAuthClientSecret, Guid>(unitOfWork), IOAuthClientSecretRepository
{
    private readonly LuckDbContextBase _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
        ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");

    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task AddAsync(OAuthClientSecret secret, CancellationToken ct = default)
    {
        _dbContext.Add(secret);
        await _unitOfWork.CommitAsync(ct);
    }
}
