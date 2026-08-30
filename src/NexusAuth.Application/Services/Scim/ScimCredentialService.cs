namespace NexusAuth.Application.Services.Scim;

public class ScimCredentialService(IScimServicePrincipalCredentialRepository credentialRepository) : IScimCredentialService
{
    public async Task<bool> ValidateAsync(string rawToken, string requiredScope, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return false;
        var credential = await credentialRepository.FindByTokenHashAsync(ScimServicePrincipalCredential.Hash(rawToken), ct);
        if (credential is null || !credential.CanAuthenticate() || !credential.Scopes.Contains(requiredScope, StringComparer.Ordinal)) return false;
        credential.RecordUse();
        await credentialRepository.UpdateAsync(credential, ct);
        return true;
    }

    public async Task<ScimCredentialCreated> CreateAsync(string name, IReadOnlyCollection<string>? scopes, DateTimeOffset? expiresAt, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (expiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow) throw new ArgumentException("expiresAt must be in the future.");
        if (await credentialRepository.FindByNameAsync(name, ct) is not null) throw new InvalidOperationException("A SCIM credential with this name already exists.");
        var created = ScimServicePrincipalCredential.Create(name, scopes ?? ["scim:read", "scim:write"], expiresAt);
        await credentialRepository.AddAsync(created.Entity, ct);
        return new ScimCredentialCreated(Map(created.Entity), created.RawToken);
    }

    public async Task<IReadOnlyList<ScimCredentialSummary>> GetAllAsync(CancellationToken ct = default) => (await credentialRepository.GetAllAsync(ct)).Select(Map).ToArray();

    public async Task<ScimCredentialSummary?> UpdateAsync(Guid id, string name, IReadOnlyCollection<string>? scopes, DateTimeOffset? expiresAt, bool isActive, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (expiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow) throw new ArgumentException("expiresAt must be in the future.");
        var credential = await credentialRepository.FindByIdAsync(id, ct);
        if (credential is null) return null;
        var sameName = await credentialRepository.FindByNameAsync(name, ct);
        if (sameName is not null && sameName.Id != id) throw new InvalidOperationException("A SCIM credential with this name already exists.");
        credential.Update(name, scopes, expiresAt);
        credential.SetActive(isActive);
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

    private static ScimCredentialSummary Map(ScimServicePrincipalCredential credential) => new(credential.Id, credential.Name, credential.Scopes, credential.IsActive, credential.ExpiresAt, credential.LastUsedAt, credential.CreatedAt, credential.RevokedAt);
}
