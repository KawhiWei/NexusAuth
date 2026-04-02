using System.Text.Json.Serialization;

namespace Demo.Bff.Models;

public record OidcFlowState(string CodeVerifier, string Nonce);

public record ValidatedIdToken(string? Subject, string? Name, string? PreferredUsername);

public record DemoUserInfo(
    [property: JsonPropertyName("sub")] string? Sub,
    [property: JsonPropertyName("preferred_username")] string? PreferredUsername,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("phone_number")] string? PhoneNumber);

public record SessionPayload(string AccessToken, string? RefreshToken, string? IdToken, int ExpiresIn, DemoUserInfo User)
{
    public DateTimeOffset IssuedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public class DiscoveryDocument
{
    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = default!;

    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; set; } = default!;

    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; } = default!;

    [JsonPropertyName("userinfo_endpoint")]
    public string UserInfoEndpoint { get; set; } = default!;

    [JsonPropertyName("jwks_uri")]
    public string JwksUri { get; set; } = default!;

    [JsonPropertyName("revocation_endpoint")]
    public string RevocationEndpoint { get; set; } = default!;
}
