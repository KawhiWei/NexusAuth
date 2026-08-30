using NexusAuth.Application.Logging;
using NexusAuth.Application.Services.Security;
using NexusAuth.Application.Services.Tokens;
using NexusAuth.Application.Services.Sessions;

namespace NexusAuth.Application.Users;

public class UserService(
    IUserRepository userRepository,
    ITokenService tokenService,
    ISsoSessionService sessionService,
    IOptions<NexusAuthSecurityOptions> securityOptions,
    ILogger<UserService> logger) : IUserService
{
    private const string DummyPasswordHash = "$2a$12$pw856E1CHH3FfcshE0NwCeETGR5hyYaeudBqZfQYCpXdbBuvOpuuy";
    private readonly NexusAuthSecurityOptions _securityOptions = securityOptions.Value;

    public async Task<Guid> RegisterAsync(
        string username,
        string rawPassword,
        string nickname,
        string? email = null,
        string? phoneNumber = null,
        Gender gender = Gender.Unknown,
        string? ethnicity = null,
        CancellationToken ct = default)
    {
        var existing = await userRepository.FindByUsernameAsync(username, ct);
        if (existing is not null)
            throw new InvalidOperationException($"Username '{username}' is already taken.");

        if (email is not null)
        {
            var byEmail = await userRepository.FindByEmailAsync(email, ct);
            if (byEmail is not null)
                throw new InvalidOperationException($"Email '{email}' is already registered.");
        }

        if (phoneNumber is not null)
        {
            var byPhone = await userRepository.FindByPhoneNumberAsync(phoneNumber, ct);
            if (byPhone is not null)
                throw new InvalidOperationException($"Phone number '{phoneNumber}' is already registered.");
        }

        var user = User.Create(username, rawPassword, nickname, email, phoneNumber, gender, ethnicity);
        await userRepository.AddAsync(user, ct);

        using (ApplicationLogScope.Begin(logger, "Authentication", user.Id.ToString(), "RegistrationSucceeded"))
        {
            logger.LogInformation("User registration succeeded. UserId={UserId}", user.Id);
        }

        return user.Id;
    }

    public async Task<User?> ValidateCredentialsAsync(
        string identifier,
        string rawPassword,
        CancellationToken ct = default)
    {
        var user = await userRepository.FindByUsernameAsync(identifier, ct);

        if (user is null && identifier.Contains('@'))
            user = await userRepository.FindByEmailAsync(identifier, ct);

        user ??= await userRepository.FindByPhoneNumberAsync(identifier, ct);

        if (user is null || !user.IsActive)
        {
            // Keep unknown/inactive accounts on the same expensive password
            // verification path so response timing does not reveal usernames.
            BCrypt.Net.BCrypt.Verify(rawPassword, DummyPasswordHash);

            using (ApplicationLogScope.Begin(logger, "Authentication", outcome: "UserNotFoundOrInactive"))
            {
                logger.LogWarning("User authentication failed. Reason={ReasonCode}", "UserNotFoundOrInactive");
            }

            return null;
        }

        var now = DateTimeOffset.UtcNow;
        if (user.IsLoginLocked(now))
        {
            BCrypt.Net.BCrypt.Verify(rawPassword, DummyPasswordHash);

            using (ApplicationLogScope.Begin(logger, "Authentication", user.Id.ToString(), "UserLocked"))
            {
                logger.LogWarning("User authentication failed. UserId={UserId} Reason={ReasonCode}", user.Id, "UserLocked");
            }

            return null;
        }

        if (!user.VerifyPassword(rawPassword))
        {
            await userRepository.RegisterFailedLoginAsync(
                user.Id,
                Math.Max(1, _securityOptions.LoginFailureLimit),
                TimeSpan.FromMinutes(Math.Max(1, _securityOptions.LoginLockoutMinutes)),
                now,
                ct);

            using (ApplicationLogScope.Begin(logger, "Authentication", user.Id.ToString(), "InvalidPassword"))
            {
                logger.LogWarning("User authentication failed. UserId={UserId} Reason={ReasonCode}", user.Id, "InvalidPassword");
            }

            return null;
        }

        await userRepository.ResetLoginFailuresAsync(user.Id, now, ct);

        using (ApplicationLogScope.Begin(logger, "Authentication", user.Id.ToString(), "LoginSucceeded"))
        {
            logger.LogInformation("User authentication succeeded. UserId={UserId}", user.Id);
        }

        return user;
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return userRepository.FindByIdAsync(id, ct);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);

        var user = await userRepository.FindByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("User was not found.");

        if (!user.IsActive)
            throw new InvalidOperationException("Inactive users cannot change their password.");

        if (!user.VerifyPassword(currentPassword))
        {
            using (ApplicationLogScope.Begin(logger, "Authentication", user.Id.ToString(), "PasswordChangeCurrentPasswordInvalid"))
            {
                logger.LogWarning("Password change rejected. UserId={UserId} Reason={ReasonCode}", user.Id, "PasswordChangeCurrentPasswordInvalid");
            }

            throw new InvalidOperationException("Current password is incorrect.");
        }

        if (user.VerifyPassword(newPassword))
            throw new InvalidOperationException("New password must differ from the current password.");

        user.ChangePassword(newPassword);
        user.InvalidateTokens(DateTimeOffset.UtcNow);
        await userRepository.UpdateAsync(user, ct);
        await tokenService.RevokeAllUserTokensAsync(user.Id, ct);
        await sessionService.RevokeAllForUserAsync(user.Id, ct);

        using (ApplicationLogScope.Begin(logger, "Authentication", user.Id.ToString(), "PasswordChanged"))
        {
            logger.LogInformation("User password changed. UserId={UserId}", user.Id);
        }
    }
}
