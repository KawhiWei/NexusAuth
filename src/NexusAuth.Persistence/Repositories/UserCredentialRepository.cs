using Luck.EntityFrameworkCore.DbContexts;
using Luck.EntityFrameworkCore.Repositories;
using Luck.Framework.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Persistence.Repositories;

public sealed class UserCredentialRepository(IUnitOfWork unitOfWork)
    : EfCoreEntityRepository<UserCredential, Guid>(unitOfWork), IUserCredentialRepository
{
    private readonly LuckDbContextBase dbContext = unitOfWork.GetLuckDbContext() as LuckDbContextBase
        ?? throw new InvalidOperationException("Failed to resolve LuckDbContext.");
    private readonly IUnitOfWork unitOfWork = unitOfWork;

    public async Task AddAsync(UserCredential credential, CancellationToken ct = default)
    {
        dbContext.Add(credential);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<UserCredential>> GetEnabledTotpAsync(Guid userId, CancellationToken ct = default) =>
        await FindAll(credential => credential.UserId == userId
                && credential.Type == UserCredential.TotpType
                && credential.IsEnabled)
            .OrderBy(credential => credential.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UserCredential>> GetTotpAsync(Guid userId, CancellationToken ct = default) =>
        await FindAll(credential => credential.UserId == userId && credential.Type == UserCredential.TotpType)
            .OrderByDescending(credential => credential.IsEnabled)
            .ThenBy(credential => credential.CreatedAt)
            .ToListAsync(ct);

    public Task<UserCredential?> FindPendingTotpAsync(Guid userId, string protectedSecret, CancellationToken ct = default) =>
        FindAll(credential => credential.UserId == userId
                && credential.Type == UserCredential.TotpType
                && !credential.IsEnabled
                && credential.PendingSecretProtected == protectedSecret)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> TryConfirmTotpAsync(Guid credentialId, Guid userId, string expectedProtectedSecret, long counter, DateTimeOffset now, CancellationToken ct = default)
    {
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE nexusauth.user_credentials
            SET secret_protected = pending_secret_protected, pending_secret_protected = NULL,
                pending_expires_at = NULL, is_enabled = true, last_used_counter = {counter}, updated_at = {now}
            WHERE id = {credentialId} AND user_id = {userId} AND type = 'totp' AND is_enabled = false
              AND pending_secret_protected = {expectedProtectedSecret}
              AND pending_expires_at IS NOT NULL AND pending_expires_at > {now};
            """, ct);
        return affected == 1;
    }

    public async Task<bool> TryUseTotpCounterAsync(Guid credentialId, Guid userId, long counter, DateTimeOffset now, CancellationToken ct = default)
    {
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE nexusauth.user_credentials SET last_used_counter = {counter}, updated_at = {now}
            WHERE id = {credentialId} AND user_id = {userId} AND type = 'totp' AND is_enabled = true
              AND secret_protected IS NOT NULL
              AND (last_used_counter IS NULL OR last_used_counter < {counter});
            """, ct);
        return affected == 1;
    }

    public async Task<bool> DisableAsync(Guid credentialId, Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var affected = await dbContext.Set<UserCredential>()
            .Where(credential => credential.Id == credentialId && credential.UserId == userId && credential.IsEnabled)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(credential => credential.IsEnabled, false)
                .SetProperty(credential => credential.SecretProtected, (string?)null)
                .SetProperty(credential => credential.LastUsedCounter, (long?)null)
                .SetProperty(credential => credential.DisabledAt, now)
                .SetProperty(credential => credential.UpdatedAt, now), ct);
        return affected == 1;
    }
}
