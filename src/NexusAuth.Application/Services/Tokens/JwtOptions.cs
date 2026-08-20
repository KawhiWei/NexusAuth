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

    public int AccessTokenLifetimeMinutes { get; set; } = 60;

    public int DeviceCodeLifetimeMinutes { get; set; } = 15;

    public int RefreshTokenLifetimeMinutes { get; set; } = 43200;
}
