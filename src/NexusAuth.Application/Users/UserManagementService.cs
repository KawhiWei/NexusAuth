namespace NexusAuth.Application.Users;

using NexusAuth.Application.Logging;
using NexusAuth.Application.Services.Sessions;

public class UserManagementService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ISsoSessionService sessionService,
    ILogger<UserManagementService> logger) : IUserManagementService
{
    public async Task<PagedResult<ManagedUserDto>> GetPagedAsync(string? keyword, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
        var (items, total) = await userRepository.GetAdminPagedAsync(keyword, isActive, normalizedPage, normalizedPageSize, ct);
        return new PagedResult<ManagedUserDto>(items.Select(Map).ToList(), total, normalizedPage, normalizedPageSize);
    }

    public async Task<ManagedUserDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct);
        return user is null ? null : Map(user);
    }

    public async Task<ManagedUserDto> UpdateProfileAsync(Guid id, UpdateManagedUserRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Nickname);
        var user = await userRepository.FindByIdAsync(id, ct) ?? throw new InvalidOperationException("User was not found.");
        if (user.IsSystemAccount) throw new InvalidOperationException("System accounts cannot be modified.");
        await EnsureEmailUniqueAsync(request.Email, user.Id, ct);
        user.UpdateScimProfile(user.Username, request.Nickname, user.IsActive, user.ExternalId, request.Email, request.PhoneNumber,
            request.GivenName, request.FamilyName, user.MiddleName, user.HonorificPrefix, user.HonorificSuffix, user.ProfileUrl,
            request.Title, request.UserType, request.PreferredLanguage, request.Locale, request.Timezone);
        await userRepository.UpdateAsync(user, ct);
        return Map(user);
    }

    public async Task<ManagedUserDto> UpdateStatusAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct) ?? throw new InvalidOperationException("User was not found.");
        if (user.IsSystemAccount) throw new InvalidOperationException("System accounts cannot be modified.");
        user.UpdateScimProfile(user.Username, user.Nickname, isActive, user.ExternalId, user.Email, user.PhoneNumber,
            user.GivenName, user.FamilyName, user.MiddleName, user.HonorificPrefix, user.HonorificSuffix, user.ProfileUrl,
            user.Title, user.UserType, user.PreferredLanguage, user.Locale, user.Timezone);
        if (!isActive)
            user.InvalidateTokens(DateTimeOffset.UtcNow);
        await userRepository.UpdateAsync(user, ct);
        if (!isActive)
        {
            await refreshTokenRepository.RevokeAllForUserAsync(user.Id, ct);
            await sessionService.RevokeAllForUserAsync(user.Id, ct);
        }
        return Map(user);
    }

    public async Task ResetPasswordAsync(Guid id, ResetManagedUserPasswordRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NewPassword);

        var user = await userRepository.FindByIdAsync(id, ct) ?? throw new InvalidOperationException("User was not found.");
        if (user.IsSystemAccount)
            throw new InvalidOperationException("System account passwords cannot be reset.");

        user.ChangePassword(request.NewPassword);
        user.InvalidateTokens(DateTimeOffset.UtcNow);
        await userRepository.UpdateAsync(user, ct);
        await refreshTokenRepository.RevokeAllForUserAsync(user.Id, ct);
        await sessionService.RevokeAllForUserAsync(user.Id, ct);

        using (ApplicationLogScope.Begin(logger, "UserManagement", user.Id.ToString(), "PasswordResetByAdministrator"))
        {
            logger.LogInformation("User password reset by an administrator. UserId={UserId}", user.Id);
        }
    }

    private async Task EnsureEmailUniqueAsync(string? email, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        var existing = await userRepository.FindByEmailAsync(email, ct);
        if (existing is not null && existing.Id != userId) throw new InvalidOperationException("Email is already used by another user.");
    }

    private static ManagedUserDto Map(User user) => new(user.Id, user.Username, user.Nickname, user.Email, user.PhoneNumber,
        user.IsActive, user.ExternalId, user.GivenName, user.FamilyName, user.Title, user.UserType, user.PreferredLanguage,
        user.Locale, user.Timezone, user.IsSystemAccount, user.CreatedAt, user.UpdatedAt);
}
