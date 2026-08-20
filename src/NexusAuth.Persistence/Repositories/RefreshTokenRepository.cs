using Luck.EntityFrameworkCore.DbContexts;
using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public class RefreshTokenRepository : EfCoreEntityRepository<RefreshToken, Guid>, IRefreshTokenRepository
{
    private readonly LuckDbContextBase _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
            ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");
    }

    public async Task<RefreshToken?> FindByTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        return await FindByTokenHashAsync(RefreshToken.Hash(token), ct);
    }

    public async Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            return null;

        return await FindAll(r => r.TokenHash == tokenHash)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        _dbContext.Add(token);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct = default)
    {
        await _dbContext.Set<RefreshToken>()
            .Where(r => r.Id == id && !r.IsRevoked)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(r => r.IsRevoked, true),
                ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        await _dbContext.Set<RefreshToken>()
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(r => r.IsRevoked, true),
                ct);
    }

    public async Task RevokeAllForClientAsync(string clientId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        await _dbContext.Set<RefreshToken>()
            .Where(r => r.ClientId == clientId && !r.IsRevoked)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(r => r.IsRevoked, true),
                ct);
    }

    public Task<RefreshToken?> RotateAsync(
        string tokenHash,
        string clientId,
        RefreshToken replacement,
        CancellationToken ct = default)
    {
        return RotateAsync(tokenHash, clientId, replacement, DateTimeOffset.UtcNow, ct);
    }

    public async Task<RefreshToken?> RotateAsync(
        string tokenHash,
        string clientId,
        RefreshToken replacement,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(replacement);

        if (!string.Equals(replacement.ClientId, clientId, StringComparison.Ordinal))
            throw new ArgumentException("The replacement token must belong to the requested client.", nameof(replacement));

        var transaction = _dbContext.Database.CurrentTransaction;
        var ownsTransaction = transaction is null;
        if (ownsTransaction)
            transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            // PostgreSQL locks the matching row for this conditional UPDATE.
            // Concurrent rotation therefore observes zero affected rows after
            // the first transaction commits.
            var affected = await _dbContext.Set<RefreshToken>()
                .Where(r => r.TokenHash == tokenHash
                    && r.ClientId == clientId
                    && !r.IsRevoked
                    && r.ExpiresAt > now)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(r => r.IsRevoked, true),
                    ct);

            if (affected != 1)
            {
                if (ownsTransaction)
                    await transaction!.RollbackAsync(ct);

                return null;
            }

            _dbContext.Add(replacement);
            await _unitOfWork.CommitAsync(ct);

            if (ownsTransaction)
                await transaction!.CommitAsync(ct);

            return replacement;
        }
        catch
        {
            if (ownsTransaction)
                await transaction!.RollbackAsync(CancellationToken.None);

            throw;
        }
        finally
        {
            if (ownsTransaction)
                await transaction!.DisposeAsync();
        }
    }
}
