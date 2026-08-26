using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace NexusAuth.Application.Services.Tokens;

public class RsaTokenSigningCredentialsProvider : ITokenSigningCredentialsProvider, IDisposable
{
    private readonly X509Certificate2? _certificate;
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _key;
    private readonly SigningCredentials _credentials;

    public RsaTokenSigningCredentialsProvider(IHostEnvironment environment, IOptions<JwtOptions> jwtOptions)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(jwtOptions);

        var options = jwtOptions.Value ?? throw new InvalidOperationException("JWT options are not configured.");
        var material = options.SigningMode switch
        {
            TokenSigningMode.Certificate => LoadCertificateMaterial(environment, options),
            TokenSigningMode.RsaKeyFile => LoadRsaKeyFileMaterial(environment, options),
            _ => throw new InvalidOperationException(
                $"Unsupported JWT signing mode '{options.SigningMode}'. Supported values are '{TokenSigningMode.Certificate}' and '{TokenSigningMode.RsaKeyFile}'."),
        };

        _certificate = material.Certificate;
        _rsa = material.Rsa;
        _key = new RsaSecurityKey(_rsa)
        {
            KeyId = material.KeyId,
        };
        _credentials = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256);
    }

    public string Algorithm => SecurityAlgorithms.RsaSha256;

    public string KeyId => _key.KeyId ?? string.Empty;

    /// <summary>
    /// 返回 JWT 签名凭据。
    /// 主要调用方：TokenService，用于签发 access_token 和 id_token。
    /// </summary>
    public SigningCredentials GetSigningCredentials() => _credentials;

    /// <summary>
    /// 生成令牌校验参数。
    /// 主要调用方：TokenService、introspection、自身撤销校验，以及外部资源服务器 JWT 校验。
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

    private static SigningMaterial LoadCertificateMaterial(IHostEnvironment environment, JwtOptions options)
    {
        var isDevelopment = environment.IsDevelopment();
        var configuredPath = isDevelopment
            ? options.DevelopmentSigningCertificatePath
            : options.SigningCertificatePath;
        var certificatePassword = isDevelopment
            ? options.DevelopmentSigningCertificatePassword
            : options.SigningCertificatePassword;
        var certificatePath = ResolvePath(environment.ContentRootPath, configuredPath, "JWT signing certificate");
        var loaded = LoadAndValidateCertificate(certificatePath, certificatePassword, isDevelopment);
        return new SigningMaterial(loaded.Rsa, loaded.Certificate, ComputeKeyId(loaded.Certificate));
    }

    private static SigningMaterial LoadRsaKeyFileMaterial(IHostEnvironment environment, JwtOptions options)
    {
        var isDevelopment = environment.IsDevelopment();
        var configuredPath = isDevelopment
            ? options.DevelopmentSigningKeyPath
            : options.SigningKeyPath;
        var keyFilePath = ResolvePath(environment.ContentRootPath, configuredPath, "JWT signing RSA key file");

        if (!File.Exists(keyFilePath))
        {
            if (!isDevelopment)
                throw new FileNotFoundException(
                    $"JWT signing RSA key file was not found at '{keyFilePath}'. "
                    + "Provision a signing key before starting outside the Development environment.",
                    keyFilePath);

            CreateSigningKeyFile(keyFilePath);
        }

        PersistedSigningKey persistedKey;
        try
        {
            var json = File.ReadAllText(keyFilePath);
            persistedKey = JsonSerializer.Deserialize<PersistedSigningKey>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Signing key file is invalid.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Signing key file '{keyFilePath}' contains invalid JSON.", exception);
        }

        if (string.IsNullOrWhiteSpace(persistedKey.KeyId))
            throw new InvalidOperationException($"Signing key file '{keyFilePath}' must contain a non-empty KeyId.");

        if (string.IsNullOrWhiteSpace(persistedKey.PrivateKeyPkcs8))
            throw new InvalidOperationException($"Signing key file '{keyFilePath}' must contain a non-empty PrivateKeyPkcs8 value.");

        var rsa = ImportPrivateKey(persistedKey.PrivateKeyPkcs8, keyFilePath);
        return new SigningMaterial(rsa, null, persistedKey.KeyId);
    }

    private static string ResolvePath(string contentRootPath, string configuredPath, string settingName)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            throw new InvalidOperationException($"{settingName} path is not configured.");

        if (Path.IsPathRooted(configuredPath))
            return configuredPath;

        return Path.Combine(contentRootPath, configuredPath);
    }

    private static (X509Certificate2 Certificate, RSA Rsa) LoadAndValidateCertificate(
        string certificatePath,
        string? certificatePassword,
        bool allowCertificateGeneration)
    {
        if (!File.Exists(certificatePath))
        {
            if (!allowCertificateGeneration)
                throw new FileNotFoundException(
                    $"JWT signing certificate was not found at '{certificatePath}'. "
                    + "Provision a signing certificate before starting outside the Development environment.",
                    certificatePath);

            CreateSigningCertificate(certificatePath, certificatePassword);
        }

        X509Certificate2? certificate = null;
        RSA? rsa = null;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12FromFile(
                certificatePath,
                certificatePassword);

            if (!certificate.HasPrivateKey)
                throw new InvalidOperationException("JWT signing certificate does not contain a private key.");

            rsa = certificate.GetRSAPrivateKey();
            if (rsa is null)
                throw new InvalidOperationException("JWT signing certificate does not contain an RSA private key.");

            if (rsa.KeySize < 2048)
                throw new InvalidOperationException($"JWT signing certificate RSA key must be at least 2048 bits (actual: {rsa.KeySize}).");

            var keyUsage = certificate.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
            if (keyUsage is not null && !keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
                throw new InvalidOperationException("JWT signing certificate KeyUsage must include DigitalSignature.");

            var now = DateTime.UtcNow;
            if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
                throw new InvalidOperationException(
                    $"JWT signing certificate is not currently valid (valid from {certificate.NotBefore:u} to {certificate.NotAfter:u}).");

            return (certificate, rsa);
        }
        catch
        {
            rsa?.Dispose();
            certificate?.Dispose();
            throw;
        }
    }

    private static RSA ImportPrivateKey(string encodedPrivateKey, string keyFilePath)
    {
        byte[] privateKey;
        try
        {
            privateKey = Convert.FromBase64String(encodedPrivateKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"Signing key file '{keyFilePath}' contains an invalid base64 PrivateKeyPkcs8 value.",
                exception);
        }

        var rsa = RSA.Create();
        try
        {
            try
            {
                // Existing files use ExportRSAPrivateKey (PKCS#1), despite the legacy field name.
                rsa.ImportRSAPrivateKey(privateKey, out _);
            }
            catch (CryptographicException)
            {
                try
                {
                    rsa.ImportPkcs8PrivateKey(privateKey, out _);
                }
                catch (CryptographicException exception)
                {
                    throw new InvalidOperationException(
                        $"Signing key file '{keyFilePath}' does not contain a valid RSA private key.",
                        exception);
                }
            }

            if (rsa.KeySize < 2048)
                throw new InvalidOperationException(
                    $"Signing key file '{keyFilePath}' contains an RSA key smaller than 2048 bits (actual: {rsa.KeySize}).");

            return rsa;
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }

    private static void CreateSigningKeyFile(string keyFilePath)
    {
        var directory = Path.GetDirectoryName(keyFilePath)
            ?? throw new InvalidOperationException("Signing key directory is invalid.");
        Directory.CreateDirectory(directory);

        using var rsa = RSA.Create(2048);
        var persistedKey = new PersistedSigningKey(
            Guid.NewGuid().ToString("N"),
            Convert.ToBase64String(rsa.ExportRSAPrivateKey()));
        var json = JsonSerializer.Serialize(persistedKey, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(keyFilePath, json);
        RestrictFilePermissions(keyFilePath);
    }

    private static void CreateSigningCertificate(string certificatePath, string? password)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(certificatePath) ?? throw new InvalidOperationException("JWT signing certificate directory is invalid."));

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=NexusAuth Token Signing",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(5));
        var pfx = certificate.Export(X509ContentType.Pfx, password ?? string.Empty);
        File.WriteAllBytes(certificatePath, pfx);
        RestrictFilePermissions(certificatePath);
    }

    private static void RestrictFilePermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static string ComputeKeyId(X509Certificate2 certificate)
        => Base64UrlEncoder.Encode(SHA256.HashData(certificate.RawData));

    public void Dispose()
    {
        _rsa.Dispose();
        _certificate?.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record SigningMaterial(RSA Rsa, X509Certificate2? Certificate, string KeyId);

    private sealed record PersistedSigningKey(string KeyId, string PrivateKeyPkcs8);
}
