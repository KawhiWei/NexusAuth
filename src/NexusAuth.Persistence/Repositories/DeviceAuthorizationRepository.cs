using Luck.EntityFrameworkCore.DbContexts;
using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public class DeviceAuthorizationRepository : EfCoreEntityRepository<DeviceAuthorization, Guid>, IDeviceAuthorizationRepository
{
    private readonly LuckDbContextBase _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public DeviceAuthorizationRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
            ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");
    }

    public async Task<DeviceAuthorization?> FindByDeviceCodeAsync(string deviceCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
            return null;

        return await FindByDeviceCodeHashAsync(DeviceAuthorization.Hash(deviceCode), ct);
    }

    public async Task<DeviceAuthorization?> FindByDeviceCodeHashAsync(string deviceCodeHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceCodeHash))
            return null;

        return await FindAll(d => d.DeviceCodeHash == deviceCodeHash)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<DeviceAuthorization?> FindByUserCodeAsync(string normalizedUserCode, CancellationToken ct = default)
    {
        return await FindAll(d => d.UserCodeNormalized == normalizedUserCode).FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(DeviceAuthorization authorization, CancellationToken ct = default)
    {
        _dbContext.Add(authorization);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task UpdateAsync(DeviceAuthorization authorization, CancellationToken ct = default)
    {
        _dbContext.Set<DeviceAuthorization>().Update(authorization);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task<bool> ApprovePendingAsync(
        Guid id,
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var affected = await _dbContext.Set<DeviceAuthorization>()
            .Where(d => d.Id == id
                && d.Status == DeviceAuthorizationStatus.Pending
                && d.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(d => d.UserId, userId)
                    .SetProperty(d => d.Status, DeviceAuthorizationStatus.Approved),
                ct);

        return affected == 1;
    }

    public async Task<bool> DenyPendingAsync(
        Guid id,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var affected = await _dbContext.Set<DeviceAuthorization>()
            .Where(d => d.Id == id
                && d.Status == DeviceAuthorizationStatus.Pending
                && d.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(d => d.Status, DeviceAuthorizationStatus.Denied),
                ct);

        return affected == 1;
    }

    public async Task<bool> TryRegisterPollAsync(
        Guid id,
        DateTimeOffset? expectedLastPolledAt,
        DateTimeOffset now,
        int pollingIntervalSeconds,
        CancellationToken ct = default)
    {
        var query = _dbContext.Set<DeviceAuthorization>()
            .Where(d => d.Id == id
                && d.Status == DeviceAuthorizationStatus.Pending
                && d.ExpiresAt > now);

        query = expectedLastPolledAt.HasValue
            ? query.Where(d => d.LastPolledAt == expectedLastPolledAt.Value)
            : query.Where(d => d.LastPolledAt == null);

        var affected = await query.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(d => d.LastPolledAt, now)
                .SetProperty(d => d.PollingIntervalSeconds, pollingIntervalSeconds),
            ct);

        return affected == 1;
    }

    public async Task<DeviceAuthorization?> ConsumeApprovedAsync(
        string deviceCodeHash,
        string clientId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceCodeHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        // Read the user/scope before the conditional update, then use a
        // status-and-client guarded update as the single consumption boundary.
        var candidate = await FindByDeviceCodeHashAsync(deviceCodeHash, ct);
        if (candidate is null)
            return null;

        var affected = await _dbContext.Set<DeviceAuthorization>()
            .Where(d => d.DeviceCodeHash == deviceCodeHash
                && d.ClientId == clientId
                && d.Status == DeviceAuthorizationStatus.Approved
                && d.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(d => d.Status, DeviceAuthorizationStatus.Consumed),
                ct);

        return affected == 1 ? candidate : null;
    }
}
