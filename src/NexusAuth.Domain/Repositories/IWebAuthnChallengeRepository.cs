using NexusAuth.Domain.Entities;

namespace NexusAuth.Domain.Repositories;

public interface IWebAuthnChallengeRepository : IEntityRepository<WebAuthnChallenge, Guid>, IScopedDependency
{
    Task AddAsync(WebAuthnChallenge challenge, CancellationToken ct = default);
    Task<WebAuthnChallenge?> TryConsumeAsync(string token, string purpose, DateTimeOffset now, CancellationToken ct = default);
    Task UpdateAsync(WebAuthnChallenge challenge, CancellationToken ct = default);
}
