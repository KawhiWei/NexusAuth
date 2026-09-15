using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NexusAuth.Application.Services.Tokens;
using Xunit;

namespace NexusAuth.Host.IntegrationTests;

public sealed class IdTokenHintValidatorTests : IDisposable
{
    private const string Issuer = "https://issuer.example";
    private const string ClientId = "test-client";
    private readonly RSA _currentRsa = RSA.Create(2048);
    private readonly RSA _previousRsa = RSA.Create(2048);

    [Fact]
    public void Expired_id_token_is_valid_as_a_logout_hint()
    {
        var validator = CreateValidator();
        var token = CreateToken(
            CreateCredentials(_currentRsa, "current"),
            "id_token",
            ClientId,
            DateTime.UtcNow.AddMinutes(-5));

        var result = validator.Validate(token);

        Assert.True(result.IsValid);
        Assert.Equal(ClientId, result.ClientId);
        Assert.Equal("subject-1", result.Subject);
    }

    [Fact]
    public void Id_token_signed_by_a_retained_previous_key_is_valid()
    {
        var validator = CreateValidator();
        var token = CreateToken(
            CreateCredentials(_previousRsa, "previous"),
            "id_token",
            ClientId,
            DateTime.UtcNow.AddMinutes(5));

        var result = validator.Validate(token);

        Assert.True(result.IsValid);
        Assert.Equal(ClientId, result.ClientId);
    }

    [Theory]
    [InlineData("access_token", ClientId)]
    [InlineData("id_token", "different-audience")]
    public void Non_id_token_or_mismatched_audience_is_rejected(string tokenUse, string audience)
    {
        var validator = CreateValidator();
        var token = CreateToken(
            CreateCredentials(_currentRsa, "current"),
            tokenUse,
            audience,
            DateTime.UtcNow.AddMinutes(5));

        var result = validator.Validate(token);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Token_with_an_unknown_signature_is_rejected()
    {
        using var unknownRsa = RSA.Create(2048);
        var validator = CreateValidator();
        var token = CreateToken(
            CreateCredentials(unknownRsa, "unknown"),
            "id_token",
            ClientId,
            DateTime.UtcNow.AddMinutes(5));

        Assert.False(validator.Validate(token).IsValid);
    }

    private IdTokenHintValidator CreateValidator()
    {
        var provider = new TestSigningCredentialsProvider(
            CreateKey(_currentRsa, "current"),
            CreateKey(_previousRsa, "previous"));
        return new IdTokenHintValidator(
            provider,
            Options.Create(new JwtOptions { Issuer = Issuer }),
            NullLogger<IdTokenHintValidator>.Instance);
    }

    private static string CreateToken(
        SigningCredentials credentials,
        string tokenUse,
        string audience,
        DateTime expires)
    {
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "subject-1"),
                new Claim("client_id", ClientId),
                new Claim("token_use", tokenUse),
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: expires,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static SigningCredentials CreateCredentials(RSA rsa, string keyId)
        => new(CreateKey(rsa, keyId), SecurityAlgorithms.RsaSha256);

    private static RsaSecurityKey CreateKey(RSA rsa, string keyId)
        => new(rsa) { KeyId = keyId };

    public void Dispose()
    {
        _currentRsa.Dispose();
        _previousRsa.Dispose();
    }

    private sealed class TestSigningCredentialsProvider(params SecurityKey[] validationKeys)
        : ITokenSigningCredentialsProvider
    {
        public string Algorithm => SecurityAlgorithms.RsaSha256;

        public string KeyId => validationKeys[0].KeyId!;

        public SigningCredentials GetSigningCredentials()
            => new(validationKeys[0], SecurityAlgorithms.RsaSha256);

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
                IssuerSigningKeys = validationKeys,
                ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                ValidAudience = audience,
                ValidateLifetime = validateLifetime,
                ClockSkew = TimeSpan.Zero,
            };
        }

        public object GetJwk() => throw new NotSupportedException();
    }
}
