namespace NexusAuth.Application.Services.Tokens;

[Obsolete("Use TokenSigningCredentialsProvider. Signing key loading is now provided by ITokenSigningKeySource strategies.")]
public sealed class RsaTokenSigningCredentialsProvider : TokenSigningCredentialsProvider
{
    public RsaTokenSigningCredentialsProvider(
        IHostEnvironment environment,
        IOptions<JwtOptions> jwtOptions)
        : base(
            environment,
            jwtOptions,
            [new CertificateTokenSigningKeySource(), new RsaKeyFileTokenSigningKeySource()])
    {
    }
}
