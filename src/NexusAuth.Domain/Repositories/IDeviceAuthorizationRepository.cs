using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

public interface IDeviceAuthorizationRepository : IEntityRepository<DeviceAuthorization, Guid>, IScopedDependency
{
    Task<DeviceAuthorization?> FindByDeviceCodeAsync(string deviceCode, CancellationToken ct = default);

    Task<DeviceAuthorization?> FindByUserCodeAsync(string normalizedUserCode, CancellationToken ct = default);

    Task AddAsync(DeviceAuthorization authorization, CancellationToken ct = default);

    Task UpdateAsync(DeviceAuthorization authorization, CancellationToken ct = default);
}
