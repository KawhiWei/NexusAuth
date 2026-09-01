namespace NexusAuth.Workbench.Api;

/// <summary>
/// Describes the resource and client metadata managed by the Workbench startup bootstrapper.
/// Authentication endpoints and credentials remain in the Auth configuration section.
/// </summary>
public sealed class WorkbenchBootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string? ResourceName { get; init; }

    public string? ResourceDisplayName { get; init; }

    public string? ResourceDescription { get; init; }

    public string? AllowedScopes { get; init; }

    public string? ClientName { get; init; }

    public string? ClientDescription { get; init; }
}
