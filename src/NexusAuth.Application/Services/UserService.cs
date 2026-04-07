using NexusAuth.Domain.AggregateRoots.Users;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// 注册本地用户账号。
    /// </summary>
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
        var existing = await _userRepository.FindByUsernameAsync(username, ct);
        if (existing is not null)
            throw new InvalidOperationException($"Username '{username}' is already taken.");

        if (email is not null)
        {
            var byEmail = await _userRepository.FindByEmailAsync(email, ct);
            if (byEmail is not null)
                throw new InvalidOperationException($"Email '{email}' is already registered.");
        }

        if (phoneNumber is not null)
        {
            var byPhone = await _userRepository.FindByPhoneNumberAsync(phoneNumber, ct);
            if (byPhone is not null)
                throw new InvalidOperationException($"Phone number '{phoneNumber}' is already registered.");
        }

        var user = User.Create(username, rawPassword, nickname, email, phoneNumber, gender, ethnicity);
        await _userRepository.AddAsync(user, ct);

        return user.Id;
    }

    /// <summary>
    /// 校验用户名/邮箱/手机号 + 密码，返回有效用户。
    /// </summary>
    public async Task<User?> ValidateCredentialsAsync(
        string identifier,
        string rawPassword,
        CancellationToken ct = default)
    {
        // Try username first
        var user = await _userRepository.FindByUsernameAsync(identifier, ct);

        // Try email
        if (user is null && identifier.Contains('@'))
            user = await _userRepository.FindByEmailAsync(identifier, ct);

        // Try phone number
        user ??= await _userRepository.FindByPhoneNumberAsync(identifier, ct);

        if (user is null || !user.IsActive)
            return null;

        return user.VerifyPassword(rawPassword) ? user : null;
    }

    /// <summary>
    /// 按用户 ID 查询用户。
    /// </summary>
    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return _userRepository.FindByIdAsync(id, ct);
    }
}
