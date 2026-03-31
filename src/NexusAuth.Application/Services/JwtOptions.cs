namespace NexusAuth.Application.Services;

public class JwtOptions
{
    public string SigningKey { get; set; } = default!;

    public string Issuer { get; set; } = default!;

    public string Audience { get; set; } = default!;

    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
