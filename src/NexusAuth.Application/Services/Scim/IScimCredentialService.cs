using Luck.Framework.Infrastructure.DependencyInjectionModule;

namespace NexusAuth.Application.Services.Scim;

public interface IScimCredentialService : IScopedDependency
{
    Task<bool> ValidateAsync(string rawToken, string requiredScope, CancellationToken ct = default);
    Task<ScimCredentialCreated> CreateAsync(string name, IReadOnlyCollection<string>? scopes, DateTimeOffset? expiresAt, CancellationToken ct = default);
    Task<IReadOnlyList<ScimCredentialSummary>> GetAllAsync(CancellationToken ct = default);
    Task<ScimCredentialSummary?> UpdateAsync(Guid id, string name, IReadOnlyCollection<string>? scopes, DateTimeOffset? expiresAt, bool isActive, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid id, CancellationToken ct = default);
}

public sealed record ScimCredentialCreated(ScimCredentialSummary Credential, string Token);
public sealed record ScimCredentialSummary(Guid Id, string Name, IReadOnlyList<string> Scopes, bool IsActive, DateTimeOffset? ExpiresAt, DateTimeOffset? LastUsedAt, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);
