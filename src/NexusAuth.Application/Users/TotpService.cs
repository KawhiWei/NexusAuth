using System.Globalization;

namespace NexusAuth.Application.Users;

public sealed class TotpService(
    IUserRepository userRepository,
    IUserCredentialRepository credentialRepository,
    ITotpSecretProtector secretProtector,
    IOptions<TotpOptions> options) : ITotpService
{
    private readonly TotpOptions _options = options.Value;

    public async Task<TotpEnrollment> BeginEnrollmentAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("User was not found.");
        if (!user.IsActive)
            throw new InvalidOperationException("Inactive users cannot configure TOTP.");

        var now = DateTimeOffset.UtcNow;
        var secret = TotpAlgorithm.Base32Encode(RandomNumberGenerator.GetBytes(20));
        var protectedSecret = secretProtector.Protect(secret);
        var expiresAt = now.AddMinutes(_options.EnrollmentLifetimeMinutes);
        var credential = UserCredential.CreatePendingTotp(user.Id, protectedSecret, expiresAt, now: now);
        await credentialRepository.AddAsync(credential, ct);

        var issuer = string.IsNullOrWhiteSpace(_options.Issuer) ? "NexusAuth" : _options.Issuer.Trim();
        var label = Uri.EscapeDataString($"{issuer}:{user.Username}");
        var uri = $"otpauth://totp/{label}?secret={secret}"
            + $"&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";
        return new TotpEnrollment(credential.Id, protectedSecret, secret, uri);
    }

    public async Task<bool> ConfirmEnrollmentAsync(
        Guid userId,
        string protectedSecret,
        string code,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var credential = await credentialRepository.FindPendingTotpAsync(userId, protectedSecret, ct);
        if (credential is null || credential.PendingExpiresAt <= now)
        {
            return false;
        }

        if (!TryUnprotect(protectedSecret, out var secret)
            || !TotpAlgorithm.TryFindCounter(secret, code, now, out var counter))
        {
            return false;
        }

        return await credentialRepository.TryConfirmTotpAsync(
            credential.Id,
            userId,
            protectedSecret,
            counter,
            now,
            ct);
    }

    public async Task<bool> IsEnabledAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(userId, ct);
        return user is { IsActive: true }
            && (await credentialRepository.GetEnabledTotpAsync(userId, ct)).Count > 0;
    }

    public async Task<bool> ValidateAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(userId, ct);
        if (user is not { IsActive: true })
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var credential in await credentialRepository.GetEnabledTotpAsync(userId, ct))
        {
            if (string.IsNullOrWhiteSpace(credential.SecretProtected)
                || !TryUnprotect(credential.SecretProtected, out var secret)
                || !TotpAlgorithm.TryFindCounter(secret, code, now, out var counter))
            {
                continue;
            }

            if (await credentialRepository.TryUseTotpCounterAsync(credential.Id, userId, counter, now, ct))
                return true;
        }

        return false;
    }

    public async Task<IReadOnlyList<TotpCredentialSummary>> GetCredentialsAsync(Guid userId, CancellationToken ct = default)
    {
        return (await credentialRepository.GetTotpAsync(userId, ct))
            .Select(credential => new TotpCredentialSummary(
                credential.Id,
                credential.DisplayName,
                credential.IsEnabled,
                credential.CreatedAt))
            .ToArray();
    }

    public Task<bool> DisableAsync(Guid userId, Guid credentialId, CancellationToken ct = default)
    {
        return credentialRepository.DisableAsync(credentialId, userId, DateTimeOffset.UtcNow, ct);
    }

    private bool TryUnprotect(string protectedSecret, out string secret)
    {
        try
        {
            secret = secretProtector.Unprotect(protectedSecret);
            return !string.IsNullOrWhiteSpace(secret);
        }
        catch (CryptographicException)
        {
            secret = string.Empty;
            return false;
        }
    }
}

public static class TotpAlgorithm
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static bool TryFindCounter(
        string base32Secret,
        string? code,
        DateTimeOffset timestamp,
        out long counter)
    {
        counter = 0;
        var normalizedCode = code?.Trim();
        if (normalizedCode is not { Length: 6 } || normalizedCode.Any(ch => ch is < '0' or > '9'))
            return false;

        byte[] key;
        try
        {
            key = Base32Decode(base32Secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var currentCounter = timestamp.ToUnixTimeSeconds() / 30;
        var supplied = Encoding.ASCII.GetBytes(normalizedCode);
        for (var offset = -1; offset <= 1; offset++)
        {
            var candidateCounter = currentCounter + offset;
            if (candidateCounter < 0)
                continue;

            var expected = Encoding.ASCII.GetBytes(ComputeCode(key, candidateCounter));
            if (CryptographicOperations.FixedTimeEquals(supplied, expected))
            {
                counter = candidateCounter;
                return true;
            }
        }

        return false;
    }

    public static string ComputeCode(string base32Secret, long counter)
    {
        return ComputeCode(Base32Decode(base32Secret), counter);
    }

    public static string Base32Encode(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return string.Empty;

        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                output.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
            output.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 31]);

        return output.ToString();
    }

    private static string ComputeCode(byte[] key, long counter)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);
        var digest = HMACSHA1.HashData(key, counterBytes);
        var offset = digest[^1] & 0x0f;
        var binaryCode = ((digest[offset] & 0x7f) << 24)
            | (digest[offset + 1] << 16)
            | (digest[offset + 2] << 8)
            | digest[offset + 3];
        return (binaryCode % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Decode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>(normalized.Length * 5 / 8);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var ch in normalized)
        {
            var index = Base32Alphabet.IndexOf(ch);
            if (index < 0)
                throw new FormatException("The TOTP secret is not valid Base32.");

            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xff));
                bitsLeft -= 8;
            }
        }

        return output.ToArray();
    }
}
