using Microsoft.AspNetCore.DataProtection;
using NexusAuth.Application.Users;

namespace NexusAuth.Host.Authentication;

public sealed class TotpSecretProtector(IDataProtectionProvider dataProtectionProvider) : ITotpSecretProtector
{
    private const string Purpose = "NexusAuth.Totp.Secret.v1";
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(Purpose);

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);
        return _protector.Unprotect(protectedValue);
    }
}
