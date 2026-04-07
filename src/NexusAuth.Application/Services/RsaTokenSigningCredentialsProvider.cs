using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace NexusAuth.Application.Services;

public class RsaTokenSigningCredentialsProvider : ITokenSigningCredentialsProvider
{
    private readonly RsaSecurityKey _key;
    private readonly SigningCredentials _credentials;

    public RsaTokenSigningCredentialsProvider()
    {
        var rsa = RSA.Create(2048);
        _key = new RsaSecurityKey(rsa)
        {
            KeyId = Guid.NewGuid().ToString("N"),
        };
        _credentials = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256);
    }

    public string Algorithm => SecurityAlgorithms.RsaSha256;

    public string KeyId => _key.KeyId ?? string.Empty;

    /// <summary>
    /// 获取 JWT 签名凭据。
    /// </summary>
    public SigningCredentials GetSigningCredentials() => _credentials;

    /// <summary>
    /// 生成令牌验证参数。
    /// </summary>
    public TokenValidationParameters CreateTokenValidationParameters(string issuer, string? audience = null, bool validateLifetime = true)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _key,
            ValidateAudience = !string.IsNullOrWhiteSpace(audience),
            ValidAudience = audience,
            ValidateLifetime = validateLifetime,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    }

    /// <summary>
    /// 导出公钥 JWK（用于 JWKS 端点）。
    /// </summary>
    public object GetJwk()
    {
        var parameters = _key.Rsa?.ExportParameters(false) ?? throw new InvalidOperationException("RSA key is unavailable.");
        return new
        {
            kty = "RSA",
            use = "sig",
            kid = KeyId,
            alg = "RS256",
            n = Base64UrlEncoder.Encode(parameters.Modulus),
            e = Base64UrlEncoder.Encode(parameters.Exponent),
        };
    }
}
