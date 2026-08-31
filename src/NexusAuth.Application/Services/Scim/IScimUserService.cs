using Luck.Framework.Infrastructure.DependencyInjectionModule;
using System.Text.Json;

namespace NexusAuth.Application.Services.Scim;

public interface IScimUserService : IScopedDependency
{
    Task<ScimUser?> FindAsync(Guid id, CancellationToken ct = default);
    Task<ScimUserList> ListAsync(string? filter, int startIndex, int count, CancellationToken ct = default);
    Task<ScimUser> CreateAsync(ScimUserInput input, CancellationToken ct = default);
    Task<ScimUser?> ReplaceAsync(Guid id, ScimUserInput input, string? expectedVersion, CancellationToken ct = default);
    Task<ScimUser?> PatchAsync(Guid id, IReadOnlyList<ScimPatchOperation> operations, string? expectedVersion, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, string? expectedVersion, CancellationToken ct = default);
}

public sealed record ScimUserList(int TotalResults, int StartIndex, IReadOnlyList<ScimUser> Resources);

public sealed record ScimUser(
    Guid Id, string UserName, string? ExternalId, bool Active, string? Email, string? PhoneNumber,
    string? GivenName, string? FamilyName, string? MiddleName, string? HonorificPrefix, string? HonorificSuffix,
    string? ProfileUrl, string? Title, string? UserType, string? PreferredLanguage, string? Locale,
    string? Timezone, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record ScimUserInput(
    string? UserName, string? ExternalId, bool? Active, string? Email, string? PhoneNumber,
    string? GivenName, string? FamilyName, string? MiddleName, string? HonorificPrefix, string? HonorificSuffix,
    string? ProfileUrl, string? Title, string? UserType, string? PreferredLanguage, string? Locale, string? Timezone);

public sealed record ScimPatchOperation(string Op, string? Path, JsonElement? Value);
