using System.Security.Cryptography.X509Certificates;

namespace NexusAuth.Application.Services.Tokens;

public sealed class CertificateTokenSigningKeySource : ITokenSigningKeySource
{
    public const string SourceName = "Certificate";

    public string Name => SourceName;

    public TokenSigningKeyMaterial Load(TokenSigningKeySourceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = context.Options;
        var certificatePath = TokenSigningKeySourceUtilities.ResolvePath(
            context.ContentRootPath,
            options.Path,
            "JWT signing certificate");

        if (!File.Exists(certificatePath))
        {
            if (!context.IsDevelopment || !options.CreateIfMissing || !options.IsActive)
                throw new FileNotFoundException($"JWT signing certificate was not found at '{certificatePath}'.", certificatePath);

            CreateSigningCertificate(certificatePath, options.Password);
        }

        var certificate = LoadCertificate(certificatePath, options.Password);
        try
        {
            using var publicRsa = certificate.GetRSAPublicKey();
            if (publicRsa is null)
                throw new InvalidOperationException("JWT signing certificate does not contain an RSA public key.");
            if (publicRsa.KeySize < 2048)
                throw new InvalidOperationException($"JWT signing certificate RSA key must be at least 2048 bits (actual: {publicRsa.KeySize}).");

            var keyUsage = certificate.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
            if (keyUsage is not null && !keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
                throw new InvalidOperationException("JWT signing certificate KeyUsage must include DigitalSignature.");

            SigningCredentials? credentials = null;
            if (options.IsActive)
            {
                using var privateRsa = certificate.GetRSAPrivateKey();
                if (!certificate.HasPrivateKey || privateRsa is null)
                    throw new InvalidOperationException("The active JWT signing certificate does not contain an RSA private key.");

                var now = DateTime.UtcNow;
                if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
                    throw new InvalidOperationException(
                        $"JWT signing certificate is not currently valid (valid from {certificate.NotBefore:u} to {certificate.NotAfter:u}).");
            }

            var keyId = string.IsNullOrWhiteSpace(options.KeyId)
                ? Base64UrlEncoder.Encode(SHA256.HashData(certificate.RawData))
                : options.KeyId;
            var securityKey = new X509SecurityKey(certificate) { KeyId = keyId };
            if (options.IsActive)
                credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

            return new TokenSigningKeyMaterial(
                keyId,
                SecurityAlgorithms.RsaSha256,
                securityKey,
                TokenSigningKeySourceUtilities.CreateRsaJwk(keyId, publicRsa.ExportParameters(false)),
                credentials,
                certificate);
        }
        catch
        {
            certificate.Dispose();
            throw;
        }
    }

    private static X509Certificate2 LoadCertificate(string path, string? password)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".pfx", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".p12", StringComparison.OrdinalIgnoreCase)
            ? X509CertificateLoader.LoadPkcs12FromFile(path, password)
            : X509CertificateLoader.LoadCertificateFromFile(path);
    }

    private static void CreateSigningCertificate(string certificatePath, string? password)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(certificatePath)
            ?? throw new InvalidOperationException("JWT signing certificate directory is invalid."));

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
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx, password ?? string.Empty));
        TokenSigningKeySourceUtilities.RestrictFilePermissions(certificatePath);
    }
}
