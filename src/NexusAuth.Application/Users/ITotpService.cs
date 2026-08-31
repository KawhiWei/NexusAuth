namespace NexusAuth.Application.Users;

/// <summary>
/// TOTP enrollment and verification operations used by the login flow.
/// </summary>
public interface ITotpService : IScopedDependency
{
    Task<TotpEnrollment> BeginEnrollmentAsync(Guid userId, CancellationToken ct = default);

    Task<bool> ConfirmEnrollmentAsync(
        Guid userId,
        string protectedSecret,
        string code,
        CancellationToken ct = default);

    Task<bool> IsEnabledAsync(Guid userId, CancellationToken ct = default);

    Task<bool> ValidateAsync(Guid userId, string code, CancellationToken ct = default);

    Task<IReadOnlyList<TotpCredentialSummary>> GetCredentialsAsync(Guid userId, CancellationToken ct = default);

    Task<bool> DisableAsync(Guid userId, Guid credentialId, CancellationToken ct = default);
}

/// <summary>
/// Enrollment material. ProtectedSecret is the Data Protection ciphertext
/// that must be supplied when confirming the enrollment; ManualEntryKey is
/// the Base32 value shown to an authenticator and OtpauthUri is its QR payload.
/// </summary>
public sealed record TotpEnrollment(
    Guid CredentialId,
    string ProtectedSecret,
    string ManualEntryKey,
    string OtpauthUri);

public sealed record TotpCredentialSummary(Guid Id, string DisplayName, bool IsEnabled, DateTimeOffset CreatedAt);

public sealed class TotpOptions
{
    public const string SectionName = "Totp";

    public string Issuer { get; set; } = "NexusAuth";

    public int EnrollmentLifetimeMinutes { get; set; } = 10;
}
