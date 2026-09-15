namespace NexusAuth.Application.Services.Tokens;

public sealed class IdTokenHintValidator(
    ITokenSigningCredentialsProvider signingCredentialsProvider,
    IOptions<JwtOptions> jwtOptions,
    ILogger<IdTokenHintValidator> logger) : IIdTokenHintValidator
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public IdTokenHintValidationResult Validate(string idTokenHint)
    {
        if (string.IsNullOrWhiteSpace(idTokenHint))
            return IdTokenHintValidationResult.Invalid();

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        if (!handler.CanReadToken(idTokenHint))
            return IdTokenHintValidationResult.Invalid();

        try
        {
            var validationParameters = signingCredentialsProvider.CreateTokenValidationParameters(
                _jwtOptions.Issuer,
                validateLifetime: false);
            validationParameters.RequireSignedTokens = true;
            validationParameters.RequireExpirationTime = true;
            validationParameters.ValidateAudience = false;

            var principal = handler.ValidateToken(idTokenHint, validationParameters, out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwt
                || !jwt.Payload.Expiration.HasValue
                || !string.Equals(principal.FindFirst("token_use")?.Value, "id_token", StringComparison.Ordinal))
            {
                return IdTokenHintValidationResult.Invalid();
            }

            var clientId = principal.FindFirst("client_id")?.Value;
            if (string.IsNullOrWhiteSpace(clientId)
                || !jwt.Audiences.Contains(clientId, StringComparer.Ordinal))
            {
                return IdTokenHintValidationResult.Invalid();
            }

            var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return IdTokenHintValidationResult.Success(clientId, subject);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            logger.LogWarning(
                "ID token hint validation failed. Reason={ReasonCode}",
                exception.GetType().Name);
            return IdTokenHintValidationResult.Invalid();
        }
    }
}

public sealed record IdTokenHintValidationResult(bool IsValid, string? ClientId, string? Subject)
{
    public static IdTokenHintValidationResult Invalid() => new(false, null, null);

    public static IdTokenHintValidationResult Success(string clientId, string? subject) => new(true, clientId, subject);
}
