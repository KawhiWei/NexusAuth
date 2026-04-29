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
public class ClientApiResourceRepository(IUnitOfWork unitOfWork) : IClientApiResourceRepository
{
    private readonly LuckDbContextBase _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
        ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");

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

    public async Task<Dictionary<Guid, List<Guid>>> GetApiResourceIdsByClientIdsAsync(IEnumerable<Guid> clientIds, CancellationToken ct = default)
    {
        var clientIdList = clientIds.Distinct().ToList();
        if (clientIdList.Count == 0)
            return [];

        var mappings = await _dbContext.Set<ClientApiResource>()
            .Where(x => clientIdList.Contains(x.ClientId))
            .Select(x => new { x.ClientId, x.ApiResourceId })
            .ToListAsync(ct);

        return mappings
            .GroupBy(x => x.ClientId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.ApiResourceId).ToList());
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

    public async Task RemoveByApiResourceIdAsync(Guid apiResourceId, CancellationToken ct = default)
    {
        var entities = await _dbContext.Set<ClientApiResource>()
            .Where(x => x.ApiResourceId == apiResourceId)
            .ToListAsync(ct);

        if (entities.Count == 0)
            return;

        _dbContext.Set<ClientApiResource>().RemoveRange(entities);
        await _dbContext.SaveChangesAsync(ct);
    }
}
