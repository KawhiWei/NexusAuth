using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.AggregateRoots.OAuthClients;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public class OAuthClientRepository : EfCoreAggregateRootRepository<OAuthClient, Guid>, IOAuthClientRepository
{
    private readonly IUnitOfWork _unitOfWork;

    public OAuthClientRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<OAuthClient?> FindByClientIdAsync(string clientId, CancellationToken ct = default)
    {
        return await FindAll(c => c.ClientId == clientId)
            .Include(c => c.ClientSecrets)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(OAuthClient client, CancellationToken ct = default)
    {
        Add(client);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task UpdateAsync(OAuthClient client, CancellationToken ct = default)
    {
        Update(client);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task DeleteAsync(OAuthClient client, CancellationToken ct = default)
    {
        Remove(client);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task<(List<OAuthClient> Items, int Total)> GetPagedAsync(
        string? keyword,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = FindAll();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(c => c.ClientId.Contains(kw) || c.ClientName.Contains(kw));
        }

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(c => c.ClientSecrets)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<OAuthClient?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await FindAll(c => c.Id == id)
            .Include(c => c.ClientSecrets)
            .FirstOrDefaultAsync(ct);
    }
}
