using Luck.DDD.Domain.Repositories;
using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

public interface IAuthorizationCodeRepository : IEntityRepository<AuthorizationCode, Guid>, IScopedDependency
{
    /// <summary>
    /// Finds a code using its raw value. Implementations hash the value before
    /// querying the database.
    /// </summary>
    Task<AuthorizationCode?> FindByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Finds a code using the persisted SHA-256 Base64Url hash.
    /// </summary>
    Task<AuthorizationCode?> FindByCodeHashAsync(string codeHash, CancellationToken ct = default);

    Task AddAsync(AuthorizationCode code, CancellationToken ct = default);

    /// <summary>
    /// Atomically marks the matching unused, unexpired code as used. The
    /// predicate is applied by the database UPDATE; callers must treat null as
    /// a failed/consumed/expired code.
    /// </summary>
    Task<AuthorizationCode?> ConsumeByCodeHashAsync(
        string codeHash,
        string clientId,
        CancellationToken ct = default);

    Task<AuthorizationCode?> ConsumeByCodeHashAsync(
        string codeHash,
        string clientId,
        DateTimeOffset now,
        CancellationToken ct = default);

    /// <summary>
    /// Raw-value convenience wrapper around ConsumeByCodeHashAsync.
    /// </summary>
    Task<AuthorizationCode?> ConsumeAsync(
        string code,
        string clientId,
        CancellationToken ct = default);

    /// <summary>
    /// Retained for callers that need to mark an entity by id. New exchange
    /// flows should use ConsumeByCodeHashAsync so code/client/expiry checks are
    /// part of the same UPDATE.
    /// </summary>
    Task MarkUsedAsync(Guid id, CancellationToken ct = default);
}
