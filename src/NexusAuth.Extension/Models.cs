using System.Text.Json.Serialization;

namespace NexusAuth.Extension;

public class FlowState(string codeVerifier, string nonce)
{
    public string CodeVerifier { get; } = codeVerifier;
    public string Nonce { get; } = nonce;
}

public class DiscoveryDocument
{
    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("userinfo_endpoint")]
    public string UserInfoEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("jwks_uri")]
    public string JwksUri { get; set; } = string.Empty;

    [JsonPropertyName("revocation_endpoint")]
    public string RevocationEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("end_session_endpoint")]
    public string EndSessionEndpoint { get; set; } = string.Empty;
}

public class WorkbenchAuthOptions
{
    public required string Authority { get; set; }
    public required string ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public required string RedirectUri { get; set; }
    public required string PostLogoutRedirectUri { get; set; }
    public required string Scope { get; set; }
}