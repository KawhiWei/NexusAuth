using System.Text.Json.Serialization;

namespace NexusAuth.Extension;

/// <summary>
/// 中文：保存一次 OIDC 授权流程所需的临时安全状态。
/// English: Stores the temporary security state required for an OIDC authorization flow.
/// </summary>
/// <param name="codeVerifier">中文：用于 PKCE 校验的原始验证码。English: The original PKCE code verifier.</param>
/// <param name="nonce">中文：用于防止 ID Token 重放攻击的随机值。English: The random value used to prevent ID token replay attacks.</param>
public class FlowState(string codeVerifier, string nonce)
{
    /// <summary>
    /// 中文：获取本次授权流程的 PKCE 验证码。
    /// English: Gets the PKCE code verifier for this authorization flow.
    /// </summary>
    public string CodeVerifier { get; } = codeVerifier;

    /// <summary>
    /// 中文：获取本次授权流程的 OIDC nonce。
    /// English: Gets the OIDC nonce for this authorization flow.
    /// </summary>
    public string Nonce { get; } = nonce;
}

/// <summary>
/// 中文：表示从 OpenID Provider 发现端点返回的服务地址集合。
/// English: Represents the service endpoints returned by the OpenID Provider discovery endpoint.
/// </summary>
public class DiscoveryDocument
{
    /// <summary>
    /// 中文：获取或设置 OpenID Provider 的发行者标识。
    /// English: Gets or sets the issuer identifier of the OpenID Provider.
    /// </summary>
    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// 中文：获取或设置用户授权端点地址。
    /// English: Gets or sets the user authorization endpoint URL.
    /// </summary>
    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// 中文：获取或设置令牌端点地址。
    /// English: Gets or sets the token endpoint URL.
    /// </summary>
    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// 中文：获取或设置用户信息端点地址。
    /// English: Gets or sets the UserInfo endpoint URL.
    /// </summary>
    [JsonPropertyName("userinfo_endpoint")]
    public string UserInfoEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// 中文：获取或设置用于验证令牌签名的 JSON Web Key Set 地址。
    /// English: Gets or sets the JSON Web Key Set URL used to validate token signatures.
    /// </summary>
    [JsonPropertyName("jwks_uri")]
    public string JwksUri { get; set; } = string.Empty;

    /// <summary>
    /// 中文：获取或设置令牌撤销端点地址。
    /// English: Gets or sets the token revocation endpoint URL.
    /// </summary>
    [JsonPropertyName("revocation_endpoint")]
    public string RevocationEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// 中文：获取或设置令牌内省端点地址。
    /// English: Gets or sets the token introspection endpoint URL.
    /// </summary>
    [JsonPropertyName("introspection_endpoint")]
    public string IntrospectionEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// 中文：获取或设置 OpenID Provider 会话注销端点地址。
    /// English: Gets or sets the OpenID Provider session logout endpoint URL.
    /// </summary>
    [JsonPropertyName("end_session_endpoint")]
    public string EndSessionEndpoint { get; set; } = string.Empty;
}

/// <summary>
/// 中文：配置 Workbench 与 NexusAuth OpenID Provider 交互所需的 OIDC 客户端参数。
/// English: Configures the OIDC client settings used by Workbench to communicate with the NexusAuth OpenID Provider.
/// </summary>
public class WorkbenchAuthOptions
{
    /// <summary>
    /// 中文：获取或设置浏览器可访问的 OpenID Provider 基础地址。
    /// English: Gets or sets the browser-accessible base URL of the OpenID Provider.
    /// </summary>
    public required string Authority { get; set; }

    /// <summary>
    /// 中文：获取或设置服务端反向通道使用的 Provider 地址；未设置时使用 <see cref="Authority"/>。
    /// English: Gets or sets the Provider URL used for server-side backchannel calls; <see cref="Authority"/> is used when omitted.
    /// </summary>
    public string? BackchannelAuthority { get; set; }

    /// <summary>
    /// 中文：获取或设置已注册的 OIDC 客户端标识。
    /// English: Gets or sets the registered OIDC client identifier.
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// 中文：获取或设置机密客户端凭据；调用令牌或内省端点时需要该值。
    /// English: Gets or sets the confidential client credential required by token and introspection endpoints.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// 中文：获取或设置 Provider 完成授权后回调 Workbench 的地址。
    /// English: Gets or sets the Workbench callback URL used after authorization completes.
    /// </summary>
    public required string RedirectUri { get; set; }

    /// <summary>
    /// 中文：获取或设置 Provider 注销完成后的返回地址。
    /// English: Gets or sets the return URL used after Provider logout completes.
    /// </summary>
    public required string PostLogoutRedirectUri { get; set; }

    /// <summary>
    /// 中文：获取或设置授权请求的空格分隔 OIDC/OAuth 作用域。
    /// English: Gets or sets the space-delimited OIDC/OAuth scopes requested during authorization.
    /// </summary>
    public required string Scope { get; set; }

    /// <summary>
    /// 中文：获取或设置本地退出时是否同时注销 OpenID Provider 会话。
    /// English: Gets or sets whether local sign-out also signs out the OpenID Provider session.
    /// </summary>
    public bool SignOutProvider { get; set; } = true;
}
