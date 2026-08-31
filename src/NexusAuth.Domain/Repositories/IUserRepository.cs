using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.AggregateRoots.Users;

namespace NexusAuth.Domain.Repositories;

public interface IUserRepository : IAggregateRootRepository<User, Guid>, IScopedDependency
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default);

    Task<User?> FindByExternalIdAsync(string externalId, CancellationToken ct = default);

    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);

    Task<User?> FindByPhoneNumberAsync(string phoneNumber, CancellationToken ct = default);

    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<User> Items, int Total)> GetScimPagedAsync(
        string? userName,
        string? externalId,
        bool? isActive,
        string? email,
        int startIndex,
        int count,
        CancellationToken ct = default);

    Task<(IReadOnlyList<User> Items, int Total)> GetAdminPagedAsync(
        string? keyword,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task AddAsync(User user, CancellationToken ct = default);

    Task UpdateAsync(User user, CancellationToken ct = default);

    Task DeleteAsync(User user, CancellationToken ct = default);

    Task<User?> RegisterFailedLoginAsync(
        Guid userId,
        int failureLimit,
        TimeSpan lockoutDuration,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task ResetLoginFailuresAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// Atomically accepts a TOTP time-step only when it is newer than the
    /// user's previously accepted step. The compare-and-update belongs in the
    /// repository so concurrent requests cannot replay the same code.
    /// </summary>
    Task<bool> TryUseTotpCounterAsync(
        Guid userId,
        long counter,
        DateTimeOffset now,
        CancellationToken ct = default)
        => TryUseTotpCounterFallbackAsync(userId, counter, now, ct);

    /// <summary>
    /// Atomically promotes a pending enrollment to the active secret. The
    /// expected protected value makes confirmation one-shot under concurrency.
    /// </summary>
    Task<bool> TryConfirmTotpEnrollmentAsync(
        Guid userId,
        string expectedProtectedSecret,
        long counter,
        DateTimeOffset now,
        CancellationToken ct = default)
        => TryConfirmTotpEnrollmentFallbackAsync(userId, expectedProtectedSecret, counter, now, ct);

    private async Task<bool> TryUseTotpCounterFallbackAsync(
        Guid userId,
        long counter,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var user = await FindByIdAsync(userId, ct);
        if (user is null)
            return false;

        bool accepted;
        lock (user)
            accepted = user.TryUseTotpCounter(counter, now);

        if (!accepted)
            return false;

        await UpdateAsync(user, ct);
        return true;
    }

    private async Task<bool> TryConfirmTotpEnrollmentFallbackAsync(
        Guid userId,
        string expectedProtectedSecret,
        long counter,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var user = await FindByIdAsync(userId, ct);
        if (user is null)
            return false;

        bool accepted;
        lock (user)
        {
            accepted = string.Equals(
                    user.TotpPendingSecretProtected,
                    expectedProtectedSecret,
                    StringComparison.Ordinal)
                && user.ConfirmTotpEnrollment(counter, now);
        }

        if (!accepted)
            return false;

        await UpdateAsync(user, ct);
        return true;
    }
}
