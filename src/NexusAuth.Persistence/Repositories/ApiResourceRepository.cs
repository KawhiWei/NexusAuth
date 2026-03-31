using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.AggregateRoots.ApiResources;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public class ApiResourceRepository : EfCoreAggregateRootRepository<ApiResource, Guid>, IApiResourceRepository
{
    private readonly IUnitOfWork _unitOfWork;

    public ApiResourceRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResource?> FindByNameAsync(string name, CancellationToken ct = default)
    {
        return await FindAll(r => r.Name == name).FirstOrDefaultAsync(ct);
    }

    public async Task<ApiResource?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await FindAsync(id);
    }

    public async Task<IReadOnlyList<ApiResource>> GetAllActiveAsync(CancellationToken ct = default)
    {
        return await FindAll(r => r.IsActive).ToListAsync(ct);
    }

    public async Task AddAsync(ApiResource resource, CancellationToken ct = default)
    {
        Add(resource);
        await _unitOfWork.CommitAsync(ct);
    }
}
