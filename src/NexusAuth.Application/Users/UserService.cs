namespace NexusAuth.Application.Users;

public class UserService(IUserRepository userRepository) : IUserService
{
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
            return null;

        return user.VerifyPassword(rawPassword) ? user : null;
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return userRepository.FindByIdAsync(id, ct);
    }
}