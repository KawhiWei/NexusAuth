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

    /// <summary>
    /// Number of failed password attempts since the last successful login or
    /// completed lockout window.
    /// </summary>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>
    /// Temporary account lockout expiry. This is persisted so a restart or a
    /// second application instance cannot bypass the lockout.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; private set; }

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
            FailedLoginAttempts = 0,
            LockedUntil = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        return user;
    }

    public bool VerifyPassword(string rawPassword)
    {
        return BCrypt.Net.BCrypt.Verify(rawPassword, PasswordHash);
    }

    public bool IsLoginLocked(DateTimeOffset now)
    {
        return LockedUntil.HasValue && LockedUntil.Value > now;
    }

    public void RegisterFailedLogin(
        DateTimeOffset now,
        int failureLimit,
        TimeSpan lockoutDuration)
    {
        if (failureLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(failureLimit));

        if (lockoutDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lockoutDuration));

        if (IsLoginLocked(now))
            return;

        // Start a fresh counter after an earlier lockout has elapsed.
        if (LockedUntil.HasValue)
            FailedLoginAttempts = 0;

        FailedLoginAttempts++;
        if (FailedLoginAttempts >= failureLimit)
        {
            LockedUntil = now.Add(lockoutDuration);
            FailedLoginAttempts = 0;
        }

        UpdatedAt = now;
    }

    public void RegisterSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginAttempts = 0;
        LockedUntil = null;
        UpdatedAt = now;
    }
}
