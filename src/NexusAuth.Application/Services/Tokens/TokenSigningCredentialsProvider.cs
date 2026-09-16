namespace NexusAuth.Application.Services.Tokens;

public class TokenSigningCredentialsProvider : ITokenSigningCredentialsProvider, IDisposable
{
    private readonly IReadOnlyList<TokenSigningKeyMaterial> _materials;
    private readonly TokenSigningKeyMaterial _active;

    public TokenSigningCredentialsProvider(
        IHostEnvironment environment,
        IOptions<JwtOptions> jwtOptions,
        IEnumerable<ITokenSigningKeySource> sources)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(jwtOptions);
        ArgumentNullException.ThrowIfNull(sources);

        var options = jwtOptions.Value ?? throw new InvalidOperationException("JWT options are not configured.");
        var keyOptions = ResolveKeyOptions(environment, options);
        var activeOptions = keyOptions.Where(key => key.IsActive).ToArray();
        if (activeOptions.Length != 1)
            throw new InvalidOperationException("JWT signing configuration must contain exactly one active signing key.");

        var sourcesByName = sources
            .GroupBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Count() == 1
                    ? group.Single()
                    : throw new InvalidOperationException($"Multiple JWT signing key sources are registered with the name '{group.Key}'."),
                StringComparer.OrdinalIgnoreCase);

        var loaded = new List<TokenSigningKeyMaterial>(keyOptions.Count);
        try
        {
            foreach (var keyOption in keyOptions)
            {
                if (string.IsNullOrWhiteSpace(keyOption.Source))
                    throw new InvalidOperationException("Every JWT signing key must specify a source.");
                if (!sourcesByName.TryGetValue(keyOption.Source, out var source))
                    throw new InvalidOperationException(
                        $"No JWT signing key source named '{keyOption.Source}' is registered.");

                var material = source.Load(new TokenSigningKeySourceContext(
                    keyOption,
                    environment.ContentRootPath,
                    environment.IsDevelopment()));
                loaded.Add(material ?? throw new InvalidOperationException(
                    $"JWT signing key source '{source.Name}' returned no key material."));
            }

            var duplicateKeyId = loaded
                .GroupBy(material => material.KeyId, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateKeyId is not null)
                throw new InvalidOperationException($"JWT signing key id '{duplicateKeyId.Key}' is configured more than once.");

            _materials = loaded;
            _active = loaded[keyOptions.IndexOf(activeOptions[0])];
            if (_active.SigningCredentials is null)
                throw new InvalidOperationException(
                    $"The active JWT signing key '{_active.KeyId}' does not provide signing credentials.");
        }
        catch
        {
            foreach (var material in loaded)
                material.Dispose();
            throw;
        }
    }

    public string Algorithm => _active.Algorithm;

    public string KeyId => _active.KeyId;

    public SigningCredentials GetSigningCredentials() => _active.SigningCredentials!;

    public TokenValidationParameters CreateTokenValidationParameters(
        string issuer,
        string? audience = null,
        bool validateLifetime = true)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = _materials.Select(material => material.ValidationKey),
            ValidateAudience = !string.IsNullOrWhiteSpace(audience),
            ValidAudience = audience,
            ValidateLifetime = validateLifetime,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    }

    public object GetJwk() => _active.PublicJwk;

    public IReadOnlyList<object> GetJwks() => _materials.Select(material => material.PublicJwk).ToArray();

    public void Dispose()
    {
        foreach (var material in _materials)
            material.Dispose();
        GC.SuppressFinalize(this);
    }

    private static List<TokenSigningKeyOptions> ResolveKeyOptions(IHostEnvironment environment, JwtOptions options)
    {
        if (options.SigningKeys is { Count: > 0 })
            return options.SigningKeys;

        var isDevelopment = environment.IsDevelopment();
        var isCertificate = options.SigningMode == TokenSigningMode.Certificate;
        var legacyPath = isCertificate
            ? (isDevelopment ? options.DevelopmentSigningCertificatePath : options.SigningCertificatePath)
            : (isDevelopment ? options.DevelopmentSigningKeyPath : options.SigningKeyPath);
        var legacyPassword = isDevelopment
            ? options.DevelopmentSigningCertificatePassword
            : options.SigningCertificatePassword;
        return
        [
            new TokenSigningKeyOptions
            {
                Source = options.SigningMode.ToString(),
                IsActive = true,
                Path = string.IsNullOrWhiteSpace(options.SigningPath) ? legacyPath : options.SigningPath,
                Password = isCertificate ? options.SigningPassword ?? legacyPassword : null,
                CreateIfMissing = isDevelopment,
            },
        ];
    }
}
