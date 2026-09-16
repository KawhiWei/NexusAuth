namespace NexusAuth.Workbench.Api;

/// <summary>
/// Describes the OAuth client provisioned for an API gateway such as APISIX.
/// </summary>
public sealed class GatewayBootstrapOptions
{
    public const string SectionName = "GatewayBootstrap";

    public bool Enabled { get; init; }

    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }

    public string? ClientName { get; init; }

    public string? ClientDescription { get; init; }

    public string? RedirectUri { get; init; }

    public string? PostLogoutRedirectUri { get; init; }

    /// <summary>
    /// Space-delimited API resource audiences. Each audience must already exist.
    /// </summary>
    public string? AllowedScopes { get; init; }
}
