namespace Demo.Bff.Options;

public class NexusAuthBffOptions
{
    public string Authority { get; set; } = default!;

    public string ClientId { get; set; } = default!;

    public string? ClientSecret { get; set; }

    public string TokenEndpointAuthMethod { get; set; } = "client_secret_basic";

    public string? ClientAssertionPrivateKeyPem { get; set; }

    public string? ClientAssertionPrivateKeyPath { get; set; }

    public string ClientAssertionSigningAlg { get; set; } = "RS256";

    public string ClientAssertionSigningKid { get; set; } = "demo-bff-key-1";

    public string RedirectUri { get; set; } = default!;

    public string PostLogoutRedirectUri { get; set; } = default!;

    public string Scope { get; set; } = default!;
}
