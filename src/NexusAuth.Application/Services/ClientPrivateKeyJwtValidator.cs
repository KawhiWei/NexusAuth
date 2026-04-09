using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using NexusAuth.Domain.AggregateRoots.OAuthClients;

namespace NexusAuth.Application.Services;

public static class ClientPrivateKeyJwtValidator
{
    private const string SupportedAssertionAlgorithm = SecurityAlgorithms.RsaSha256;

    private static readonly JwtSecurityTokenHandler Handler = new()
    {
        // 保留 JWT 原始 claim 名称，避免 sub 被映射成 NameIdentifier 后取不到值。
        MapInboundClaims = false,
    };

    /// <summary>
    /// 校验 private_key_jwt 客户端断言。
    /// 主要调用方：ClientService，在 /connect/token、/connect/revocation、/connect/introspect 等端点统一复用。
    /// </summary>
    public static ClientAssertionValidationResult Validate(string assertionJwt, OAuthClient client, string expectedAudience)
    {
        JwtSecurityToken unvalidatedToken;
        try
        {
            unvalidatedToken = Handler.ReadJwtToken(assertionJwt);
        }
        catch (ArgumentException ex)
        {
            return ClientAssertionValidationResult.Failure($"Invalid client assertion: {ex.Message}");
        }

        if (!string.Equals(unvalidatedToken.Header.Alg, SupportedAssertionAlgorithm, StringComparison.Ordinal))
            return ClientAssertionValidationResult.Failure($"client_assertion alg must be {SupportedAssertionAlgorithm}.");

        var clientJwksJson = client.GetJwks();
        if (string.IsNullOrWhiteSpace(clientJwksJson))
            return ClientAssertionValidationResult.Failure("Client JWK set is missing.");

        JsonWebKeySet jwkSet;
        try
        {
            jwkSet = new JsonWebKeySet(clientJwksJson);
        }
        catch (JsonException)
        {
            return ClientAssertionValidationResult.Failure("Client JWK set format is invalid.");
        }

        if (jwkSet.Keys.Count == 0)
            return ClientAssertionValidationResult.Failure("Client JWK set does not contain any key.");

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = ResolveSigningKeys(jwkSet),
            ValidateIssuer = true,
            ValidIssuer = client.ClientId,
            ValidateAudience = true,
            ValidAudience = expectedAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RequireSignedTokens = true,
            RequireExpirationTime = true,
        };

        try
        {
            var principal = Handler.ValidateToken(assertionJwt, validationParameters, out var validatedToken);
            var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.Equals(subject, client.ClientId, StringComparison.Ordinal))
                return ClientAssertionValidationResult.Failure("client_assertion sub must match client_id.");

            var jwtId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrWhiteSpace(jwtId))
                return ClientAssertionValidationResult.Failure("client_assertion jti is required.");

            var expiresAt = validatedToken.ValidTo == DateTime.MinValue
                ? DateTimeOffset.UtcNow.AddMinutes(5)
                : new DateTimeOffset(validatedToken.ValidTo, TimeSpan.Zero);

            return ClientAssertionValidationResult.Success(jwtId, expiresAt, unvalidatedToken.Header.Alg);
        }
        catch (SecurityTokenException ex)
        {
            return ClientAssertionValidationResult.Failure($"Invalid client assertion: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return ClientAssertionValidationResult.Failure($"Invalid client assertion: {ex.Message}");
        }
    }

    private static IReadOnlyList<SecurityKey> ResolveSigningKeys(JsonWebKeySet jwkSet)
    {
        var keys = new List<SecurityKey>();

        foreach (var key in jwkSet.Keys)
        {
            if (!string.Equals(key.Kty, "RSA", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.IsNullOrWhiteSpace(key.N) || string.IsNullOrWhiteSpace(key.E))
                continue;

            RSAParameters parameters;
            try
            {
                parameters = new RSAParameters
                {
                    Modulus = Base64UrlEncoder.DecodeBytes(key.N),
                    Exponent = Base64UrlEncoder.DecodeBytes(key.E),
                };
            }
            catch (CryptographicException)
            {
                continue;
            }

            keys.Add(new RsaSecurityKey(parameters)
            {
                KeyId = string.IsNullOrWhiteSpace(key.Kid) ? null : key.Kid,
            });
        }

        return keys;
    }
}

public record ClientAssertionValidationResult(
    bool IsSuccess,
    string? Error,
    string? Jti = null,
    DateTimeOffset? ExpiresAt = null,
    string? Algorithm = null)
{
    public static ClientAssertionValidationResult Success(string jti, DateTimeOffset expiresAt, string algorithm)
        => new(true, null, jti, expiresAt, algorithm);

    public static ClientAssertionValidationResult Failure(string error) => new(false, error);
}
