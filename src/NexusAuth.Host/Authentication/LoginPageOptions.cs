namespace NexusAuth.Host.Authentication;

public sealed class LoginPageOptions
{
    public const string SectionName = "LoginPage";

    public string BrandName { get; set; } = "NexusAuth";

    public string BrandLogoUrl { get; set; } = "/brand/nexusauth-logo.svg";

    public string MarketingHeading { get; set; } = "基于 OAuth 2.1 与 OIDC 的统一身份认证";

    public string MarketingDescription { get; set; } = "支持 OAuth 2.1、OpenID Connect（OIDC）、授权码 + PKCE 和 SCIM 2.0 标准协议。";

    public string LoginTitle { get; set; } = "Sign In";

    public string LoginSubtitle { get; set; } = "Please enter your details to sign in.";
}
