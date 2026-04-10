using Luck.EntityFrameworkCore.DbContexts;
using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public class DeviceAuthorizationRepository(IUnitOfWork unitOfWork) : EfCoreEntityRepository<DeviceAuthorization, Guid>(unitOfWork), IDeviceAuthorizationRepository
{
    private readonly LuckDbContextBase _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
        ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");

    public async Task<DeviceAuthorization?> FindByDeviceCodeAsync(string deviceCode, CancellationToken ct = default)
    {
        return await FindAll(d => d.DeviceCode == deviceCode).FirstOrDefaultAsync(ct);
    }

    public async Task<DeviceAuthorization?> FindByUserCodeAsync(string normalizedUserCode, CancellationToken ct = default)
    {
        return await FindAll(d => d.UserCodeNormalized == normalizedUserCode).FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(DeviceAuthorization authorization, CancellationToken ct = default)
    {
        await _dbContext.Set<DeviceAuthorization>().AddAsync(authorization, ct);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task UpdateAsync(DeviceAuthorization authorization, CancellationToken ct = default)
    {
        _dbContext.Set<DeviceAuthorization>().Update(authorization);
        await unitOfWork.CommitAsync(ct);
    }
}
