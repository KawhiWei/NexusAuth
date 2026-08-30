using Luck.Framework.Infrastructure.DependencyInjectionModule;

namespace NexusAuth.Application.Users;

public interface IUserManagementService : IScopedDependency
{
    Task<PagedResult<ManagedUserDto>> GetPagedAsync(string? keyword, bool? isActive, int page, int pageSize, CancellationToken ct = default);
    Task<ManagedUserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ManagedUserDto> UpdateProfileAsync(Guid id, UpdateManagedUserRequest request, CancellationToken ct = default);
    Task<ManagedUserDto> UpdateStatusAsync(Guid id, bool isActive, CancellationToken ct = default);
    Task ResetPasswordAsync(Guid id, ResetManagedUserPasswordRequest request, CancellationToken ct = default);
}

public sealed record ManagedUserDto(
    Guid Id, string Username, string Nickname, string? Email, string? PhoneNumber, bool IsActive,
    string? ExternalId, string? GivenName, string? FamilyName, string? Title, string? UserType,
    string? PreferredLanguage, string? Locale, string? Timezone, bool IsSystemAccount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record UpdateManagedUserRequest(
    string Nickname, string? Email, string? PhoneNumber, string? GivenName, string? FamilyName,
    string? Title, string? UserType, string? PreferredLanguage, string? Locale, string? Timezone);

public sealed record ResetManagedUserPasswordRequest(string NewPassword);
