namespace NexusAuth.Application.Services.Security;

public class NexusAuthSecurityOptions
{
    public List<string> ClientWhitelist { get; set; } = [];

    public List<string> ClientBlacklist { get; set; } = [];

    public List<string> UserWhitelist { get; set; } = [];

    public List<string> UserBlacklist { get; set; } = [];
}
