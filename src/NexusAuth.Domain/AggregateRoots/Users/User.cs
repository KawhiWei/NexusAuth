using Luck.DDD.Domain.Domain.AggregateRoots;

namespace NexusAuth.Domain.AggregateRoots.Users;

public class User : AggregateRootWithIdentity<Guid>
{
    public string Username { get; private set; } = default!;

    /// <summary>
    /// Stable identifier supplied by an upstream SCIM provisioning client.
    /// </summary>
    public string? ExternalId { get; private set; }

    public string PasswordHash { get; private set; } = default!;

    public string? Email { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string Nickname { get; private set; } = default!;

    public string? GivenName { get; private set; }

    public string? FamilyName { get; private set; }

    public string? MiddleName { get; private set; }

    public string? HonorificPrefix { get; private set; }

    public string? HonorificSuffix { get; private set; }

    public string? ProfileUrl { get; private set; }

    public string? Title { get; private set; }

    public string? UserType { get; private set; }

    public string? PreferredLanguage { get; private set; }

    public string? Locale { get; private set; }

    public string? Timezone { get; private set; }

    public Gender Gender { get; private set; }

    public string? Ethnicity { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsSystemAccount { get; private set; }

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
            IsSystemAccount = false,
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

    public void ChangePassword(string rawPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPassword);

        PasswordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword, workFactor: 12);
        UpdatedAt = DateTimeOffset.UtcNow;
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

    public void UpdateScimProfile(
        string username,
        string nickname,
        bool isActive,
        string? externalId = null,
        string? email = null,
        string? phoneNumber = null,
        string? givenName = null,
        string? familyName = null,
        string? middleName = null,
        string? honorificPrefix = null,
        string? honorificSuffix = null,
        string? profileUrl = null,
        string? title = null,
        string? userType = null,
        string? preferredLanguage = null,
        string? locale = null,
        string? timezone = null)
    {
        if (IsSystemAccount)
            throw new InvalidOperationException("System accounts cannot be modified.");
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(nickname);

        Username = username.Trim();
        Nickname = nickname.Trim();
        IsActive = isActive;
        ExternalId = NormalizeNullable(externalId);
        Email = NormalizeNullable(email)?.ToLowerInvariant();
        PhoneNumber = NormalizeNullable(phoneNumber);
        GivenName = NormalizeNullable(givenName);
        FamilyName = NormalizeNullable(familyName);
        MiddleName = NormalizeNullable(middleName);
        HonorificPrefix = NormalizeNullable(honorificPrefix);
        HonorificSuffix = NormalizeNullable(honorificSuffix);
        ProfileUrl = NormalizeNullable(profileUrl);
        Title = NormalizeNullable(title);
        UserType = NormalizeNullable(userType);
        PreferredLanguage = NormalizeNullable(preferredLanguage);
        Locale = NormalizeNullable(locale);
        Timezone = NormalizeNullable(timezone);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsSystemAccount()
    {
        IsSystemAccount = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
