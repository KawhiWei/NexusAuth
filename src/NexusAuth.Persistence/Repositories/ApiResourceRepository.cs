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

    /// <summary>
    /// 按资源名称查询单个 API 资源。
    /// </summary>
    public async Task<ApiResource?> FindByNameAsync(string name, CancellationToken ct = default)
    {
        return await FindAll(r => r.Name == name).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// 按资源名称批量查询 API 资源。
    /// </summary>
    public async Task<IReadOnlyList<ApiResource>> FindByNamesAsync(IEnumerable<string> names, CancellationToken ct = default)
    {
        var nameSet = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (nameSet.Length == 0)
            return [];

        return await FindAll(r => nameSet.Contains(r.Name)).ToListAsync(ct);
    }

    /// <summary>
    /// 按主键查询 API 资源。
    /// </summary>
    public async Task<ApiResource?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await FindAsync(id);
    }

    /// <summary>
    /// 获取全部启用状态的 API 资源。
    /// </summary>
    public async Task<IReadOnlyList<ApiResource>> GetAllActiveAsync(CancellationToken ct = default)
    {
        return await FindAll(r => r.IsActive).ToListAsync(ct);
    }

    /// <summary>
    /// 新增 API 资源并提交事务。
    /// </summary>
    public async Task AddAsync(ApiResource resource, CancellationToken ct = default)
    {
        Add(resource);
        await _unitOfWork.CommitAsync(ct);
    }
}
