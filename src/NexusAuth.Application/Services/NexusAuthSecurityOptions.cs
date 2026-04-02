namespace NexusAuth.Application.Services;

public class NexusAuthSecurityOptions
{
    public List<string> ClientWhitelist { get; set; } = [];

    public List<string> ClientBlacklist { get; set; } = [];

    public List<string> UserWhitelist { get; set; } = [];

    public List<string> UserBlacklist { get; set; } = [];
}
