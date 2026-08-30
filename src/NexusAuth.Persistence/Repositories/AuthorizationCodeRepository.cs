using Luck.EntityFrameworkCore.DbContexts;
using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public class AuthorizationCodeRepository : EfCoreEntityRepository<AuthorizationCode, Guid>, IAuthorizationCodeRepository
{
    private readonly LuckDbContextBase _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public AuthorizationCodeRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
            ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");
    }

    public async Task<AuthorizationCode?> FindByCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return await FindByCodeHashAsync(AuthorizationCode.Hash(code), ct);
    }

    public async Task<AuthorizationCode?> FindByCodeHashAsync(string codeHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codeHash))
            return null;

        return await FindAll(a => a.CodeHash == codeHash)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(AuthorizationCode code, CancellationToken ct = default)
    {
        _dbContext.Add(code);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task MarkUsedAsync(Guid id, CancellationToken ct = default)
    {
        await _dbContext.Set<AuthorizationCode>()
            .Where(a => a.Id == id && !a.IsUsed && a.ExpiresAt > DateTimeOffset.UtcNow)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(a => a.IsUsed, true),
                ct);
    }

    public Task<AuthorizationCode?> ConsumeByCodeHashAsync(
        string codeHash,
        string clientId,
        CancellationToken ct = default)
    {
        return ConsumeByCodeHashAsync(codeHash, clientId, DateTimeOffset.UtcNow, ct);
    }

    public async Task<AuthorizationCode?> ConsumeByCodeHashAsync(
        string codeHash,
        string clientId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        // The one conditional UPDATE is the concurrency boundary. A second
        // request waits for the row lock and then observes IsUsed = true.
        var affected = await _dbContext.Set<AuthorizationCode>()
            .Where(a => a.CodeHash == codeHash
                && a.ClientId == clientId
                && !a.IsUsed
                && a.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(a => a.IsUsed, true),
                ct);

        if (affected != 1)
            return null;

        return await FindByCodeHashAsync(codeHash, ct);
    }

    public Task<AuthorizationCode?> ConsumeAsync(
        string code,
        string clientId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Task.FromResult<AuthorizationCode?>(null);

        return ConsumeByCodeHashAsync(AuthorizationCode.Hash(code), clientId, ct);
    }
}
