using NexusAuth.Domain.Entities;

namespace NexusAuth.Application.Services.OpenApi;

public interface IOpenApiCredentialService : IScopedDependency
{
    Task<bool> ValidateAsync(string rawToken, string targetType, CancellationToken ct = default);
    Task<OpenApiCredentialCreated> CreateAsync(string name, string targetType, DateTimeOffset? expiresAt, CancellationToken ct = default);
    Task<IReadOnlyList<OpenApiCredentialSummary>> GetAllAsync(CancellationToken ct = default);
    Task<OpenApiCredentialSummary?> UpdateAsync(Guid id, string name, DateTimeOffset? expiresAt, bool isActive, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid id, CancellationToken ct = default);
}

public sealed record OpenApiCredentialCreated(OpenApiCredentialSummary Credential, string Token);
public sealed record OpenApiCredentialSummary(Guid Id, string Name, string TargetType, IReadOnlyList<string> Scopes, bool IsActive, DateTimeOffset? ExpiresAt, DateTimeOffset? LastUsedAt, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);
