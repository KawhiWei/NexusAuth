using Luck.EntityFrameworkCore.DbContexts;
using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public sealed class WebAuthnChallengeRepository(IUnitOfWork unitOfWork)
    : EfCoreEntityRepository<WebAuthnChallenge, Guid>(unitOfWork), IWebAuthnChallengeRepository
{
    private readonly LuckDbContextBase _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
        ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task AddAsync(WebAuthnChallenge challenge, CancellationToken ct = default)
    {
        _dbContext.Add(challenge);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task<WebAuthnChallenge?> TryConsumeAsync(
        string token,
        string purpose,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var tokenHash = WebAuthnChallenge.HashToken(token);
        var challenge = await FindAll(value => value.TokenHash == tokenHash && value.Purpose == purpose)
            .FirstOrDefaultAsync(ct);
        if (challenge is null || !challenge.TryConsume(now))
            return null;

        try
        {
            // xmin 乐观并发令牌保证并发请求中只有一个能够成功消费挑战。
            _dbContext.Update(challenge);
            await _unitOfWork.CommitAsync(ct);
            return challenge;
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
    }

    public async Task UpdateAsync(WebAuthnChallenge challenge, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        _dbContext.Update(challenge);
        await _unitOfWork.CommitAsync(ct);
    }
}
