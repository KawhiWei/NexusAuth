namespace NexusAuth.Application.Services.Tokens;

public sealed class TokenSigningKeyMaterial : IDisposable
{
    private readonly IDisposable? _lifetime;

    public TokenSigningKeyMaterial(
        string keyId,
        string algorithm,
        SecurityKey validationKey,
        object publicJwk,
        SigningCredentials? signingCredentials = null,
        IDisposable? lifetime = null)
    {
        if (string.IsNullOrWhiteSpace(keyId))
            throw new ArgumentException("A signing key id is required.", nameof(keyId));
        if (string.IsNullOrWhiteSpace(algorithm))
            throw new ArgumentException("A signing algorithm is required.", nameof(algorithm));

        KeyId = keyId;
        Algorithm = algorithm;
        ValidationKey = validationKey ?? throw new ArgumentNullException(nameof(validationKey));
        PublicJwk = publicJwk ?? throw new ArgumentNullException(nameof(publicJwk));
        SigningCredentials = signingCredentials;
        ValidationKey.KeyId = keyId;
        if (SigningCredentials is not null)
            SigningCredentials.Key.KeyId = keyId;
        _lifetime = lifetime;
    }

    public string KeyId { get; }

    public string Algorithm { get; }

    public SecurityKey ValidationKey { get; }

    public object PublicJwk { get; }

    public SigningCredentials? SigningCredentials { get; }

    public void Dispose() => _lifetime?.Dispose();
}
