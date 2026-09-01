namespace NexusAuth.Application.Users;

public interface ITotpSecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
