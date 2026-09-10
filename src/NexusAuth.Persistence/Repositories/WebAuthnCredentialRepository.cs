using Luck.EntityFrameworkCore.DbContexts;
using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public sealed class WebAuthnCredentialRepository(IUnitOfWork unitOfWork)
    : EfCoreEntityRepository<WebAuthnCredential, Guid>(unitOfWork), IWebAuthnCredentialRepository
{
    private readonly LuckDbContextBase _dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
        ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task AddAsync(WebAuthnCredential credential, CancellationToken ct = default)
    {
        _dbContext.Add(credential);
        await _unitOfWork.CommitAsync(ct);
    }

    public Task<WebAuthnCredential?> FindByCredentialIdAsync(byte[] credentialId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credentialId);
        return FindAll(credential => credential.CredentialId.SequenceEqual(credentialId)).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<WebAuthnCredential>> GetEnabledForUserAsync(Guid userId, CancellationToken ct = default) =>
        await FindAll(credential => credential.UserId == userId && credential.DisabledAt == null)
            .OrderBy(credential => credential.CreatedAt)
            .ToListAsync(ct);

    public async Task UpdateAsync(WebAuthnCredential credential, CancellationToken ct = default)
    {
        _dbContext.Update(credential);
        await _unitOfWork.CommitAsync(ct);
    }
}
