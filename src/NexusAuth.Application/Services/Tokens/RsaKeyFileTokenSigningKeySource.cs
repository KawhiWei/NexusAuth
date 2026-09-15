namespace NexusAuth.Application.Services.Tokens;

public sealed class RsaKeyFileTokenSigningKeySource : ITokenSigningKeySource
{
    public const string SourceName = "RsaKeyFile";

    public string Name => SourceName;

    public TokenSigningKeyMaterial Load(TokenSigningKeySourceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = context.Options;
        var keyFilePath = TokenSigningKeySourceUtilities.ResolvePath(
            context.ContentRootPath,
            options.Path,
            "JWT signing RSA key file");

        if (!File.Exists(keyFilePath))
        {
            if (!context.IsDevelopment || !options.CreateIfMissing || !options.IsActive)
                throw new FileNotFoundException($"JWT signing RSA key file was not found at '{keyFilePath}'.", keyFilePath);

            CreateSigningKeyFile(keyFilePath);
        }

        var persistedKey = ReadPersistedKey(keyFilePath);
        var keyId = string.IsNullOrWhiteSpace(options.KeyId) ? persistedKey.KeyId : options.KeyId;
        var rsa = ImportPrivateKey(persistedKey.PrivateKeyPkcs8, keyFilePath);
        try
        {
            var securityKey = new RsaSecurityKey(rsa) { KeyId = keyId };
            var credentials = options.IsActive
                ? new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256)
                : null;

            return new TokenSigningKeyMaterial(
                keyId,
                SecurityAlgorithms.RsaSha256,
                securityKey,
                TokenSigningKeySourceUtilities.CreateRsaJwk(keyId, rsa.ExportParameters(false)),
                credentials,
                rsa);
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }

    private static PersistedSigningKey ReadPersistedKey(string keyFilePath)
    {
        try
        {
            var persistedKey = JsonSerializer.Deserialize<PersistedSigningKey>(
                File.ReadAllText(keyFilePath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Signing key file is invalid.");

            if (string.IsNullOrWhiteSpace(persistedKey.KeyId))
                throw new InvalidOperationException($"Signing key file '{keyFilePath}' must contain a non-empty KeyId.");
            if (string.IsNullOrWhiteSpace(persistedKey.PrivateKeyPkcs8))
                throw new InvalidOperationException($"Signing key file '{keyFilePath}' must contain a non-empty PrivateKeyPkcs8 value.");

            return persistedKey;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Signing key file '{keyFilePath}' contains invalid JSON.", exception);
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
                // Existing files use PKCS#1 despite the legacy property name.
                rsa.ImportRSAPrivateKey(privateKey, out _);
            }
            catch (CryptographicException)
            {
                rsa.ImportPkcs8PrivateKey(privateKey, out _);
            }

            if (rsa.KeySize < 2048)
                throw new InvalidOperationException(
                    $"Signing key file '{keyFilePath}' contains an RSA key smaller than 2048 bits (actual: {rsa.KeySize}).");

            return rsa;
        }
        catch (CryptographicException exception)
        {
            rsa.Dispose();
            throw new InvalidOperationException(
                $"Signing key file '{keyFilePath}' does not contain a valid RSA private key.",
                exception);
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }

    private static void CreateSigningKeyFile(string keyFilePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(keyFilePath)
            ?? throw new InvalidOperationException("Signing key directory is invalid."));

        using var rsa = RSA.Create(2048);
        var persistedKey = new PersistedSigningKey(
            Guid.NewGuid().ToString("N"),
            Convert.ToBase64String(rsa.ExportRSAPrivateKey()));
        File.WriteAllText(keyFilePath, JsonSerializer.Serialize(
            persistedKey,
            new JsonSerializerOptions { WriteIndented = true }));
        TokenSigningKeySourceUtilities.RestrictFilePermissions(keyFilePath);
    }

    private sealed record PersistedSigningKey(string KeyId, string PrivateKeyPkcs8);
}
