using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NexusAuth.Application.Services.Tokens;
using Xunit;

namespace NexusAuth.Host.IntegrationTests;

public sealed class TokenSigningProviderTests
{
    [Theory]
    [InlineData(TokenSigningMode.Certificate, "signing.pfx")]
    [InlineData(TokenSigningMode.RsaKeyFile, "signing.json")]
    public void Unified_path_is_used_in_development_and_production(TokenSigningMode mode, string filename)
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var options = Options.Create(new JwtOptions
            {
                SigningMode = mode,
                SigningPath = Path.Combine(directory, filename),
                SigningPassword = "test-password",
                DevelopmentSigningCertificatePath = "unused-development.pfx",
                SigningCertificatePath = "unused-production.pfx",
                DevelopmentSigningKeyPath = "unused-development.json",
                SigningKeyPath = "unused-production.json",
            });
            ITokenSigningKeySource[] sources = [new CertificateTokenSigningKeySource(), new RsaKeyFileTokenSigningKeySource()];
            using var development = new TokenSigningCredentialsProvider(
                new TestHostEnvironment { EnvironmentName = "Development", ContentRootPath = directory }, options, sources);
            Assert.True(File.Exists(options.Value.SigningPath));
            using var production = new TokenSigningCredentialsProvider(
                new TestHostEnvironment { EnvironmentName = "Production", ContentRootPath = directory }, options, sources);
            Assert.Equal(development.KeyId, production.KeyId);
            Assert.NotNull(production.GetSigningCredentials());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(TokenSigningMode.Certificate)]
    [InlineData(TokenSigningMode.RsaKeyFile)]
    public void Unified_path_does_not_generate_missing_files_in_production(TokenSigningMode mode)
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var options = Options.Create(new JwtOptions
            {
                SigningMode = mode,
                SigningPath = Path.Combine(directory, "missing.pfx"),
            });
            Assert.Throws<FileNotFoundException>(() => new TokenSigningCredentialsProvider(
                new TestHostEnvironment { EnvironmentName = "Production", ContentRootPath = directory }, options,
                [new CertificateTokenSigningKeySource(), new RsaKeyFileTokenSigningKeySource()]));
            Assert.False(File.Exists(options.Value.SigningPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Certificate_source_can_publish_a_public_only_previous_certificate()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var pfxPath = Path.Combine(directory, "current.pfx");
            var cerPath = Path.Combine(directory, "previous.cer");
            CreateCertificateFiles(pfxPath, cerPath, "test-password");
            var source = new CertificateTokenSigningKeySource();

            using var active = source.Load(new TokenSigningKeySourceContext(
                new TokenSigningKeyOptions
                {
                    Source = source.Name,
                    IsActive = true,
                    Path = pfxPath,
                    Password = "test-password",
                },
                directory,
                false));
            using var previous = source.Load(new TokenSigningKeySourceContext(
                new TokenSigningKeyOptions
                {
                    Source = source.Name,
                    Path = cerPath,
                },
                directory,
                false));

            Assert.NotNull(active.SigningCredentials);
            Assert.Null(previous.SigningCredentials);
            Assert.NotEqual(active.KeyId, previous.KeyId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Rsa_key_file_source_loads_the_existing_legacy_format()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "signing-key.json");
            using var rsa = RSA.Create(2048);
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                KeyId = "rsa-file-key",
                PrivateKeyPkcs8 = Convert.ToBase64String(rsa.ExportRSAPrivateKey()),
            }));
            var source = new RsaKeyFileTokenSigningKeySource();

            using var material = source.Load(new TokenSigningKeySourceContext(
                new TokenSigningKeyOptions
                {
                    Source = source.Name,
                    IsActive = true,
                    Path = path,
                },
                directory,
                false));

            Assert.Equal("rsa-file-key", material.KeyId);
            Assert.NotNull(material.SigningCredentials);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Multiple_keys_use_the_active_key_and_publish_all_public_keys()
    {
        using var source = new TestTokenSigningKeySource();
        using var provider = CreateProvider(source, includePreviousKey: true);

        Assert.Equal("current", provider.KeyId);
        Assert.Equal("current", provider.GetSigningCredentials().Key.KeyId);

        var jwks = provider.GetJwks();
        Assert.Equal(2, jwks.Count);
        var serializedJwks = JsonSerializer.Serialize(jwks);
        Assert.Contains("\"kid\":\"current\"", serializedJwks);
        Assert.Contains("\"kid\":\"previous\"", serializedJwks);
        Assert.DoesNotContain("\"d\"", serializedJwks);
    }

    [Fact]
    public void Retained_key_validates_old_tokens_until_it_is_removed()
    {
        using var source = new TestTokenSigningKeySource();
        using var provider = CreateProvider(source, includePreviousKey: true);
        var oldToken = CreateToken(source.GetSigningCredentials("previous"));

        var principal = new JwtSecurityTokenHandler().ValidateToken(
            oldToken,
            provider.CreateTokenValidationParameters("https://issuer.example", validateLifetime: false),
            out _);

        Assert.NotNull(principal);

        using var providerAfterRemoval = CreateProvider(source, includePreviousKey: false);
        Assert.ThrowsAny<SecurityTokenException>(() => new JwtSecurityTokenHandler().ValidateToken(
            oldToken,
            providerAfterRemoval.CreateTokenValidationParameters("https://issuer.example", validateLifetime: false),
            out _));
    }

    [Fact]
    public void Configuration_requires_exactly_one_active_key()
    {
        using var source = new TestTokenSigningKeySource();
        var options = Options.Create(new JwtOptions
        {
            SigningKeys =
            [
                new TokenSigningKeyOptions { Source = source.Name, KeyId = "one" },
                new TokenSigningKeyOptions { Source = source.Name, KeyId = "two" },
            ],
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new TokenSigningCredentialsProvider(new TestHostEnvironment(), options, [source]));

        Assert.Contains("exactly one active", exception.Message);
    }

    private static TokenSigningCredentialsProvider CreateProvider(
        TestTokenSigningKeySource source,
        bool includePreviousKey)
    {
        var signingKeys = new List<TokenSigningKeyOptions>
        {
            new() { Source = source.Name, KeyId = "current", IsActive = true },
        };
        if (includePreviousKey)
            signingKeys.Add(new TokenSigningKeyOptions { Source = source.Name, KeyId = "previous" });

        return new TokenSigningCredentialsProvider(
            new TestHostEnvironment(),
            Options.Create(new JwtOptions { SigningKeys = signingKeys }),
            [source]);
    }

    private static string CreateToken(SigningCredentials credentials)
    {
        var token = new JwtSecurityToken(
            issuer: "https://issuer.example",
            claims: [],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nexusauth-signing-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateCertificateFiles(string pfxPath, string cerPath, string password)
    {
        using var currentRsa = RSA.Create(2048);
        var currentRequest = new CertificateRequest(
            "CN=NexusAuth Current Test",
            currentRsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        currentRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        using var current = currentRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllBytes(pfxPath, current.Export(X509ContentType.Pfx, password));

        using var previousRsa = RSA.Create(2048);
        var previousRequest = new CertificateRequest(
            "CN=NexusAuth Previous Test",
            previousRsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        previousRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        using var previous = previousRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-2),
            DateTimeOffset.UtcNow.AddDays(-1));
        File.WriteAllBytes(cerPath, previous.Export(X509ContentType.Cert));
    }

    private sealed class TestTokenSigningKeySource : ITokenSigningKeySource, IDisposable
    {
        private readonly Dictionary<string, RSA> _keys = new(StringComparer.Ordinal);

        public string Name => "Test";

        public TokenSigningKeyMaterial Load(TokenSigningKeySourceContext context)
        {
            var keyId = context.Options.KeyId
                ?? throw new InvalidOperationException("The test key id is required.");
            if (!_keys.TryGetValue(keyId, out var rsa))
            {
                rsa = RSA.Create(2048);
                _keys.Add(keyId, rsa);
            }

            var key = new RsaSecurityKey(rsa) { KeyId = keyId };
            var parameters = rsa.ExportParameters(false);
            var jwk = new Dictionary<string, object?>
            {
                ["kty"] = "RSA",
                ["use"] = "sig",
                ["kid"] = keyId,
                ["alg"] = "RS256",
                ["n"] = Base64UrlEncoder.Encode(parameters.Modulus),
                ["e"] = Base64UrlEncoder.Encode(parameters.Exponent),
            };

            return new TokenSigningKeyMaterial(
                keyId,
                SecurityAlgorithms.RsaSha256,
                key,
                jwk,
                context.Options.IsActive ? new SigningCredentials(key, SecurityAlgorithms.RsaSha256) : null);
        }

        public SigningCredentials GetSigningCredentials(string keyId)
        {
            var key = new RsaSecurityKey(_keys[keyId]) { KeyId = keyId };
            return new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        }

        public void Dispose()
        {
            foreach (var key in _keys.Values)
                key.Dispose();
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "NexusAuth.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
