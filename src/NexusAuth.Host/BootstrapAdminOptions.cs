namespace NexusAuth.Host;

public sealed class BootstrapAdminOptions
{
    public string? Username { get; set; }

    public string? Password { get; set; }

    public string Nickname { get; set; } = "System Admin";

    public string? Email { get; set; }
}
