using NexusAuth.Domain.AggregateRoots.Users;
using NexusAuth.Application.Services.Sessions;

namespace NexusAuth.Application.Services.Scim;

public class ScimUserService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ISsoSessionService sessionService) : IScimUserService
{
    private const int MaximumPageSize = 200;

    public async Task<ScimUser?> FindAsync(Guid id, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct);
        return user is null ? null : Map(user);
    }

    public async Task<ScimUserList> ListAsync(string? filter, int startIndex, int count, CancellationToken ct = default)
    {
        var criteria = ParseFilter(filter);
        startIndex = Math.Max(1, startIndex);
        count = Math.Clamp(count, 0, MaximumPageSize);
        var (items, total) = await userRepository.GetScimPagedAsync(
            criteria.UserName, criteria.ExternalId, criteria.Active, criteria.Email, startIndex, count, ct);
        return new ScimUserList(total, startIndex, items.Select(Map).ToArray());
    }

    public async Task<ScimUser> CreateAsync(ScimUserInput input, CancellationToken ct = default)
    {
        var username = Required(input.UserName, "userName");
        await EnsureUniqueAsync(username, input.ExternalId, input.Email, null, ct);
        var user = User.Create(username, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), username, input.Email, input.PhoneNumber);
        Apply(user, input, username);
        await userRepository.AddAsync(user, ct);
        return Map(user);
    }

    public async Task<ScimUser?> ReplaceAsync(Guid id, ScimUserInput input, string? expectedVersion, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct);
        if (user is null || !MatchesVersion(user, expectedVersion)) return null;
        var username = Required(input.UserName, "userName");
        await EnsureUniqueAsync(username, input.ExternalId, input.Email, id, ct);
        Apply(user, input, username);
        if (!user.IsActive)
            user.InvalidateTokens(DateTimeOffset.UtcNow);
        await userRepository.UpdateAsync(user, ct);
        await RevokeCredentialsIfInactiveAsync(user, ct);
        return Map(user);
    }

    public async Task<ScimUser?> PatchAsync(Guid id, IReadOnlyList<ScimPatchOperation> operations, string? expectedVersion, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct);
        if (user is null || !MatchesVersion(user, expectedVersion)) return null;
        var next = ToInput(user);
        foreach (var operation in operations)
            next = ApplyPatch(next, operation);
        var username = Required(next.UserName, "userName");
        await EnsureUniqueAsync(username, next.ExternalId, next.Email, id, ct);
        Apply(user, next, username);
        if (!user.IsActive)
            user.InvalidateTokens(DateTimeOffset.UtcNow);
        await userRepository.UpdateAsync(user, ct);
        await RevokeCredentialsIfInactiveAsync(user, ct);
        return Map(user);
    }

    public async Task<bool> DeleteAsync(Guid id, string? expectedVersion, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct);
        if (user is null || !MatchesVersion(user, expectedVersion)) return false;
        await refreshTokenRepository.RevokeAllForUserAsync(user.Id, ct);
        await sessionService.RevokeAllForUserAsync(user.Id, ct);
        await userRepository.DeleteAsync(user, ct);
        return true;
    }

    private async Task EnsureUniqueAsync(string username, string? externalId, string? email, Guid? currentId, CancellationToken ct)
    {
        var sameUsername = await userRepository.FindByUsernameAsync(username, ct);
        if (sameUsername is not null && sameUsername.Id != currentId) throw new InvalidOperationException("userName already exists.");
        if (!string.IsNullOrWhiteSpace(externalId))
        {
            var sameExternalId = await userRepository.FindByExternalIdAsync(externalId, ct);
            if (sameExternalId is not null && sameExternalId.Id != currentId) throw new InvalidOperationException("externalId already exists for this provisioning connection.");
        }
        if (!string.IsNullOrWhiteSpace(email))
        {
            var sameEmail = await userRepository.FindByEmailAsync(email, ct);
            if (sameEmail is not null && sameEmail.Id != currentId) throw new InvalidOperationException("email already exists.");
        }
    }

    private static void Apply(User user, ScimUserInput input, string username) => user.UpdateScimProfile(
        username, input.GivenName ?? input.FamilyName ?? username, input.Active ?? true, input.ExternalId, input.Email, input.PhoneNumber,
        input.GivenName, input.FamilyName, input.MiddleName, input.HonorificPrefix, input.HonorificSuffix, input.ProfileUrl,
        input.Title, input.UserType, input.PreferredLanguage, input.Locale, input.Timezone);

    private async Task RevokeCredentialsIfInactiveAsync(User user, CancellationToken ct)
    {
        if (user.IsActive)
            return;

        await refreshTokenRepository.RevokeAllForUserAsync(user.Id, ct);
        await sessionService.RevokeAllForUserAsync(user.Id, ct);
    }

    private static ScimUserInput ToInput(User user) => new(user.Username, user.ExternalId, user.IsActive, user.Email, user.PhoneNumber,
        user.GivenName, user.FamilyName, user.MiddleName, user.HonorificPrefix, user.HonorificSuffix, user.ProfileUrl, user.Title,
        user.UserType, user.PreferredLanguage, user.Locale, user.Timezone);

    private static ScimUser Map(User user) => new(user.Id, user.Username, user.ExternalId, user.IsActive, user.Email, user.PhoneNumber,
        user.GivenName, user.FamilyName, user.MiddleName, user.HonorificPrefix, user.HonorificSuffix, user.ProfileUrl, user.Title,
        user.UserType, user.PreferredLanguage, user.Locale, user.Timezone, user.CreatedAt, user.UpdatedAt);

    public static string Version(ScimUser user) => $"\"{user.UpdatedAt.UtcTicks}\"";
    private static bool MatchesVersion(User user, string? expected) => string.IsNullOrWhiteSpace(expected) || expected == "*" || expected == $"\"{user.UpdatedAt.UtcTicks}\"";
    private static string Required(string? value, string name) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentException($"{name} is required.");

    private static (string? UserName, string? ExternalId, bool? Active, string? Email) ParseFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return default;
        var match = System.Text.RegularExpressions.Regex.Match(filter.Trim(), "^(userName|externalId|active|emails\\.value)\\s+eq\\s+(?:\\\"([^\\\"]*)\\\"|true|false)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success) throw new ArgumentException("Only userName, externalId, active, and emails.value equality filters are supported.");
        var value = match.Groups[2].Success ? match.Groups[2].Value : filter.Trim().EndsWith("true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
        return match.Groups[1].Value.ToLowerInvariant() switch
        {
            "username" => (value, null, null, null), "externalid" => (null, value, null, null),
            "active" => (null, null, bool.Parse(value), null), _ => (null, null, null, value)
        };
    }

    private static ScimUserInput ApplyPatch(ScimUserInput input, ScimPatchOperation operation)
    {
        var path = operation.Path?.Trim().ToLowerInvariant();
        var remove = string.Equals(operation.Op, "remove", StringComparison.OrdinalIgnoreCase);
        if (!remove && !string.Equals(operation.Op, "add", StringComparison.OrdinalIgnoreCase) && !string.Equals(operation.Op, "replace", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("PATCH op must be add, replace, or remove.");
        var value = remove ? null : operation.Value;
        return path switch
        {
            "username" => input with { UserName = value }, "externalid" => input with { ExternalId = value }, "active" => input with { Active = remove ? true : bool.Parse(value ?? throw new ArgumentException("active requires a boolean value.")) },
            "emails.value" => input with { Email = value }, "phonenumbers.value" => input with { PhoneNumber = value }, "name.givenname" => input with { GivenName = value }, "name.familyname" => input with { FamilyName = value },
            "name.middlename" => input with { MiddleName = value }, "name.honorificprefix" => input with { HonorificPrefix = value }, "name.honorificsuffix" => input with { HonorificSuffix = value },
            "profileurl" => input with { ProfileUrl = value }, "title" => input with { Title = value }, "usertype" => input with { UserType = value }, "preferredlanguage" => input with { PreferredLanguage = value }, "locale" => input with { Locale = value }, "timezone" => input with { Timezone = value },
            _ => throw new ArgumentException("Unsupported PATCH path.")
        };
    }
}
