namespace NexusAuth.Host.Authentication;

public sealed class SelfRegistrationOptions
{
    public const string SectionName = "SelfRegistration";

    public bool Enabled { get; set; }
}
