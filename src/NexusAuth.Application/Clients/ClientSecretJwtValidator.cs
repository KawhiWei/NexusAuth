namespace NexusAuth.Application.Clients;

public static class ClientSecretJwtValidator
{
    private const string SupportedAssertionAlgorithm = SecurityAlgorithms.HmacSha256;

    private static readonly JwtSecurityTokenHandler Handler = new()
    {
        MapInboundClaims = false,
    };

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

        var secretValues = client.GetSharedSecretValues();
        if (secretValues.Count == 0)
            return ClientAssertionValidationResult.Failure("Client shared secret is missing.");

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = [.. secretValues.Select(secret => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)))],
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
}
