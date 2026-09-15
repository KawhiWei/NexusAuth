namespace NexusAuth.Application.Services.Tokens;

public enum TokenSigningMode
{
    Certificate,
    RsaKeyFile,
}

public class JwtOptions
{
    public string Issuer { get; set; } = default!;

    public string DefaultAudience { get; set; } = default!;

    public TokenSigningMode SigningMode { get; set; } = TokenSigningMode.Certificate;

    public string SigningCertificatePath { get; set; } = string.Empty;

    public string SigningCertificatePassword { get; set; } = string.Empty;

    public string DevelopmentSigningCertificatePath { get; set; } = "App_Data/development-signing-certificate.pfx";

    public string DevelopmentSigningCertificatePassword { get; set; } = string.Empty;

    public string SigningKeyPath { get; set; } = string.Empty;

    public string DevelopmentSigningKeyPath { get; set; } = "App_Data/signing-key.json";

    public List<TokenSigningKeyOptions> SigningKeys { get; set; } = [];

    public int AccessTokenLifetimeMinutes { get; set; } = 60;

    public int DeviceCodeLifetimeMinutes { get; set; } = 15;

    public int RefreshTokenLifetimeMinutes { get; set; } = 43200;
}

public class TokenSigningKeyOptions
{
    public string Source { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? KeyId { get; set; }

    public string Path { get; set; } = string.Empty;

    public string? Password { get; set; }

    public bool CreateIfMissing { get; set; }

    public Dictionary<string, string?> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
