using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

public interface IDeviceAuthorizationRepository : IEntityRepository<DeviceAuthorization, Guid>, IScopedDependency
{
    Task<DeviceAuthorization?> FindByDeviceCodeAsync(string deviceCode, CancellationToken ct = default);

    Task<DeviceAuthorization?> FindByDeviceCodeHashAsync(string deviceCodeHash, CancellationToken ct = default);

    Task<DeviceAuthorization?> FindByUserCodeAsync(string normalizedUserCode, CancellationToken ct = default);

    Task AddAsync(DeviceAuthorization authorization, CancellationToken ct = default);

    Task UpdateAsync(DeviceAuthorization authorization, CancellationToken ct = default);

    Task<bool> ApprovePendingAsync(
        Guid id,
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<bool> DenyPendingAsync(
        Guid id,
        DateTimeOffset now,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically records a poll against the expected previous timestamp.
    /// The compare-and-update prevents concurrent pollers from losing the
    /// interval/backoff update.
    /// </summary>
    Task<bool> TryRegisterPollAsync(
        Guid id,
        DateTimeOffset? expectedLastPolledAt,
        DateTimeOffset now,
        int pollingIntervalSeconds,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically consumes an approved authorization for the owning client.
    /// </summary>
    Task<DeviceAuthorization?> ConsumeApprovedAsync(
        string deviceCodeHash,
        string clientId,
        DateTimeOffset now,
        CancellationToken ct = default);
}
