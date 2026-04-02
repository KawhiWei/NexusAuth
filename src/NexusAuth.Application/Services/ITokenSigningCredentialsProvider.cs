namespace NexusAuth.Application.Services;

public interface ITokenSigningCredentialsProvider
{
    string Algorithm { get; }

    string KeyId { get; }

    Microsoft.IdentityModel.Tokens.SigningCredentials GetSigningCredentials();

    Microsoft.IdentityModel.Tokens.TokenValidationParameters CreateTokenValidationParameters(string issuer, string? audience = null, bool validateLifetime = true);

    object GetJwk();
}
