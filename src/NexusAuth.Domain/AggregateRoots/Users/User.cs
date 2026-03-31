using Luck.DDD.Domain.Domain.AggregateRoots;

namespace NexusAuth.Domain.AggregateRoots.Users;

public class User : AggregateRootWithIdentity<Guid>
{
    public string Username { get; private set; } = default!;

    public string PasswordHash { get; private set; } = default!;

    public string? Email { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string Nickname { get; private set; } = default!;

    public Gender Gender { get; private set; }

    public string? Ethnicity { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// EF Core constructor
    /// </summary>
    private User(Guid id) : base(id)
    {
    }

    public static User Create(
        string username,
        string rawPassword,
        string nickname,
        string? email = null,
        string? phoneNumber = null,
        Gender gender = Gender.Unknown,
        string? ethnicity = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(nickname);

        var now = DateTimeOffset.UtcNow;
        var user = new User(Guid.NewGuid())
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword, workFactor: 12),
            Email = email?.ToLowerInvariant(),
            PhoneNumber = phoneNumber,
            Nickname = nickname,
            Gender = gender,
            Ethnicity = ethnicity,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        return user;
    }

    public bool VerifyPassword(string rawPassword)
    {
        return BCrypt.Net.BCrypt.Verify(rawPassword, PasswordHash);
    }
}
