namespace Demo.Bff.Options;

public class MobileAuthOptions
{
    public string ClientId { get; set; } = "demo-mobile";

    public string ClientSecret { get; set; } = "demo-mobile-secret";

    public string RedirectUri { get; set; } = "myapp://auth/callback";

    public string Scope { get; set; } = "openid profile email phone offline_access demo_api";
}
