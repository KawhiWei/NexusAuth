using Luck.EntityFrameworkCore.DbContexts;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.AggregateRoots.ApiResources;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

/// <summary>
/// ClientApiResource uses composite PK, so it cannot use EfCoreEntityRepository.
/// Uses IUnitOfWork to access DbContext directly.
/// </summary>
public class ClientApiResourceRepository : IClientApiResourceRepository
{
    private readonly LuckDbContextBase _dbContext;

    public ClientApiResourceRepository(IUnitOfWork unitOfWork)
    {
        _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
                     ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");
    }

    public async Task<IReadOnlyList<ApiResource>> GetResourcesByClientIdAsync(Guid clientId, CancellationToken ct = default)
    {
        var resourceIds = await _dbContext.Set<ClientApiResource>()
            .Where(x => x.ClientId == clientId)
            .Select(x => x.ApiResourceId)
            .ToListAsync(ct);

        return await _dbContext.Set<ApiResource>()
            .Where(r => resourceIds.Contains(r.Id))
            .ToListAsync(ct);
    }

    public async Task AddAsync(ClientApiResource association, CancellationToken ct = default)
    {
        await _dbContext.Set<ClientApiResource>().AddAsync(association, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid clientId, Guid apiResourceId, CancellationToken ct = default)
    {
        var entity = await _dbContext.Set<ClientApiResource>()
            .FirstOrDefaultAsync(x => x.ClientId == clientId && x.ApiResourceId == apiResourceId, ct);

        if (entity is not null)
        {
            _dbContext.Set<ClientApiResource>().Remove(entity);
            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
