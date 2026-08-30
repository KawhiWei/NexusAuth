using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

public interface IRefreshTokenRepository : IEntityRepository<RefreshToken, Guid>, IScopedDependency
{
    /// <summary>
    /// Finds a token using its raw value. Implementations hash the value before
    /// querying the database.
    /// </summary>
    Task<RefreshToken?> FindByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Finds a token using the persisted SHA-256 Base64Url hash.
    /// </summary>
    Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    Task RevokeAsync(Guid id, CancellationToken ct = default);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);

    Task RevokeAllForClientAsync(string clientId, CancellationToken ct = default);

    /// <summary>
    /// Atomically revokes the matching active token and persists replacement
    /// in one database transaction. The replacement is returned only when the
    /// conditional UPDATE matched exactly one row; a concurrent second use
    /// therefore returns null.
    /// </summary>
    Task<RefreshToken?> RotateAsync(
        string tokenHash,
        string clientId,
        RefreshToken replacement,
        CancellationToken ct = default);

    Task<RefreshToken?> RotateAsync(
        string tokenHash,
        string clientId,
        RefreshToken replacement,
        DateTimeOffset now,
        CancellationToken ct = default);
}
