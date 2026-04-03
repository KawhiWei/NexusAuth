namespace NexusAuth.Application.Services;

public class JwtOptions
{
    public string Issuer { get; set; } = default!;

    public string DefaultAudience { get; set; } = default!;

    public int AccessTokenLifetimeMinutes { get; set; } = 60;

    public int DeviceCodeLifetimeMinutes { get; set; } = 15;
}
