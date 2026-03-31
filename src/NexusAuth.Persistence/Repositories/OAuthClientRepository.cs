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
        return await FindAll(c => c.ClientId == clientId).FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(OAuthClient client, CancellationToken ct = default)
    {
        Add(client);
        await _unitOfWork.CommitAsync(ct);
    }
}
