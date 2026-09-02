using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Application.Services.OpenApi;

public sealed class OpenApiCredentialService(IOpenApiCredentialRepository credentialRepository) : IOpenApiCredentialService
{
    public async Task<bool> ValidateAsync(string rawToken, string targetType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return false;
        var normalizedTargetType = OpenApiCredential.NormalizeTargetType(targetType);
        var credential = await credentialRepository.FindByTokenHashAsync(OpenApiCredential.Hash(rawToken), ct);
        if (credential is null || !credential.CanAuthenticate(normalizedTargetType, OpenApiCredential.GetReadScope(normalizedTargetType))) return false;
        credential.RecordUse();
        await credentialRepository.UpdateAsync(credential, ct);
        return true;
    }

    public async Task<OpenApiCredentialCreated> CreateAsync(string name, string targetType, DateTimeOffset? expiresAt, CancellationToken ct = default)
    {
        if (expiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow) throw new ArgumentException("expiresAt must be in the future.");
        if (await credentialRepository.FindByNameAsync(name, ct) is not null) throw new InvalidOperationException("An Open API credential with this name already exists.");
        var created = OpenApiCredential.Create(name, targetType, expiresAt);
        await credentialRepository.AddAsync(created.Entity, ct);
        return new(Map(created.Entity), created.Token);
    }

    public async Task<IReadOnlyList<OpenApiCredentialSummary>> GetAllAsync(CancellationToken ct = default) => (await credentialRepository.GetAllAsync(ct)).Select(Map).ToArray();
    public async Task<OpenApiCredentialSummary?> UpdateAsync(Guid id, string name, DateTimeOffset? expiresAt, bool isActive, CancellationToken ct = default)
    {
        if (expiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow) throw new ArgumentException("expiresAt must be in the future.");
        var credential = await credentialRepository.FindByIdAsync(id, ct);
        if (credential is null) return null;
        var sameName = await credentialRepository.FindByNameAsync(name, ct);
        if (sameName is not null && sameName.Id != id) throw new InvalidOperationException("An Open API credential with this name already exists.");
        credential.Update(name, expiresAt, isActive);
        await credentialRepository.UpdateAsync(credential, ct);
        return Map(credential);
    }
    public async Task<bool> RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var credential = await credentialRepository.FindByIdAsync(id, ct);
        if (credential is null) return false;
        credential.Revoke();
        await credentialRepository.UpdateAsync(credential, ct);
        return true;
    }
    private static OpenApiCredentialSummary Map(OpenApiCredential item) => new(item.Id, item.Name, item.TargetType, item.Scopes, item.IsActive, item.ExpiresAt, item.LastUsedAt, item.CreatedAt, item.RevokedAt);
}
