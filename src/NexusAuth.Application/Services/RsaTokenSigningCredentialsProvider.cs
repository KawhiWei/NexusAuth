using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace NexusAuth.Application.Services;

public class RsaTokenSigningCredentialsProvider : ITokenSigningCredentialsProvider
{
    private readonly RsaSecurityKey _key;
    private readonly SigningCredentials _credentials;

    public RsaTokenSigningCredentialsProvider(IHostEnvironment environment, IOptions<JwtOptions> jwtOptions)
    {
        var options = jwtOptions.Value;
        var keyFilePath = ResolveKeyFilePath(environment.ContentRootPath, options.SigningKeyPath);
        var persistedKey = LoadOrCreateKeyMaterial(keyFilePath);

        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(Convert.FromBase64String(persistedKey.PrivateKeyPkcs8), out _);

        _key = new RsaSecurityKey(rsa)
        {
            KeyId = persistedKey.KeyId,
        };
        _credentials = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256);
    }

    public string Algorithm => SecurityAlgorithms.RsaSha256;

    public string KeyId => _key.KeyId ?? string.Empty;

    /// <summary>
    /// 返回 JWT 签名凭据。
    /// 主要调用方：TokenService，用于签发 access_token 与 id_token。
    /// </summary>
    public SigningCredentials GetSigningCredentials() => _credentials;

    /// <summary>
    /// 生成令牌校验参数。
    /// 主要调用方：TokenService 的 introspection、自身撤销校验，以及外部资源服务器 JWT 校验。
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
    /// 导出公钥 JWK，供 JWKS 端点对外公开。
    /// 主要调用方：Host 层的 /.well-known/jwks.json。
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

    private static string ResolveKeyFilePath(string contentRootPath, string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
            return configuredPath;

        return Path.Combine(contentRootPath, configuredPath);
    }

    private static PersistedSigningKey LoadOrCreateKeyMaterial(string keyFilePath)
    {
        if (File.Exists(keyFilePath))
        {
            var existingJson = File.ReadAllText(keyFilePath);
            return JsonSerializer.Deserialize<PersistedSigningKey>(existingJson)
                   ?? throw new InvalidOperationException("Signing key file is invalid.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(keyFilePath) ?? throw new InvalidOperationException("Signing key directory is invalid."));

        using var rsa = RSA.Create(2048);
        var persistedKey = new PersistedSigningKey(
            Guid.NewGuid().ToString("N"),
            Convert.ToBase64String(rsa.ExportRSAPrivateKey()));

        var json = JsonSerializer.Serialize(persistedKey, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(keyFilePath, json);
        return persistedKey;
    }

    private sealed record PersistedSigningKey(string KeyId, string PrivateKeyPkcs8);
}
