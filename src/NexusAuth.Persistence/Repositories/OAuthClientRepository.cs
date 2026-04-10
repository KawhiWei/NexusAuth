using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.AggregateRoots.OAuthClients;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public class OAuthClientRepository(IUnitOfWork unitOfWork) : EfCoreAggregateRootRepository<OAuthClient, Guid>(unitOfWork), IOAuthClientRepository
{
    public async Task<OAuthClient?> FindByClientIdAsync(string clientId, CancellationToken ct = default)
    {
        return await FindAll(c => c.ClientId == clientId).FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(OAuthClient client, CancellationToken ct = default)
    {
        Add(client);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task UpdateAsync(OAuthClient client, CancellationToken ct = default)
    {
        Update(client);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task DeleteAsync(OAuthClient client, CancellationToken ct = default)
    {
        Remove(client);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task<List<OAuthClient>> GetAllAsync(CancellationToken ct = default)
    {
        return await FindAll().ToListAsync(ct);
    }

    public async Task<OAuthClient?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await FindAsync(id);
    }
}