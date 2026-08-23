using NexusAuth.Application.Services.Security;

namespace NexusAuth.Application.Users;

public class UserService(
    IUserRepository userRepository,
    IOptions<NexusAuthSecurityOptions> securityOptions) : IUserService
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
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        if (user.IsLoginLocked(now))
        {
            BCrypt.Net.BCrypt.Verify(rawPassword, DummyPasswordHash);
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
            return null;
        }

        await userRepository.ResetLoginFailuresAsync(user.Id, now, ct);
        return user;
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return userRepository.FindByIdAsync(id, ct);
    }
}
