namespace NexusAuth.Extension;

/// <summary>
/// 中文：定义 Workbench 执行 OIDC 授权码、刷新令牌和令牌内省流程所需的操作。
/// English: Defines the operations required by Workbench for OIDC authorization-code, token-refresh, and token-introspection flows.
/// </summary>
public interface IOidcWorkbenchService
{
    /// <summary>
    /// 中文：获取浏览器可访问的 OpenID Provider 基础地址。
    /// English: Gets the browser-accessible base URL of the OpenID Provider.
    /// </summary>
    string Authority { get; }

    /// <summary>
    /// 中文：获取已注册的 OIDC 客户端标识。
    /// English: Gets the registered OIDC client identifier.
    /// </summary>
    string ClientId { get; }

    /// <summary>
    /// 中文：获取授权完成后的回调地址。
    /// English: Gets the callback URL used after authorization completes.
    /// </summary>
    string RedirectUri { get; }

    /// <summary>
    /// 中文：获取 Provider 注销完成后的返回地址。
    /// English: Gets the return URL used after Provider logout completes.
    /// </summary>
    string PostLogoutRedirectUri { get; }

    /// <summary>
    /// 中文：获取授权请求使用的 OIDC/OAuth 作用域。
    /// English: Gets the OIDC/OAuth scopes used by authorization requests.
    /// </summary>
    string Scope { get; }

    /// <summary>
    /// 中文：获取本地退出时是否同时注销 Provider 会话。
    /// English: Gets whether local sign-out also signs out the Provider session.
    /// </summary>
    bool SignOutProvider { get; }

    /// <summary>
    /// 中文：获取用于保存授权请求临时安全状态的存储服务。
    /// English: Gets the store used to retain temporary authorization-request security state.
    /// </summary>
    IFlowStateStore FlowStateStore { get; }

    /// <summary>
    /// 中文：从 OpenID Provider 获取发现文档。
    /// English: Fetches the discovery document from the OpenID Provider.
    /// </summary>
    /// <param name="ct">中文：用于取消 HTTP 请求的令牌。English: A token used to cancel the HTTP request.</param>
    /// <returns>中文：包含 Provider 元数据和端点的发现文档。English: The discovery document containing Provider metadata and endpoints.</returns>
    Task<DiscoveryDocument> FetchDiscoveryAsync(CancellationToken ct);

    /// <summary>
    /// 中文：生成符合 PKCE 要求的高强度随机 code_verifier。
    /// English: Generates a cryptographically strong PKCE code_verifier.
    /// </summary>
    /// <returns>中文：Base64 URL 编码的 PKCE 验证码。English: A Base64 URL-encoded PKCE code verifier.</returns>
    string GenerateCodeVerifier();

    /// <summary>
    /// 中文：生成一组相互匹配的 PKCE code_challenge 和 code_verifier。
    /// English: Generates a matching PKCE code_challenge and code_verifier pair.
    /// </summary>
    /// <returns>中文：包含挑战值和原始验证码的元组。English: A tuple containing the challenge and original verifier.</returns>
    (string codeChallenge, string codeVerifier) GeneratePkce();

    /// <summary>
    /// 中文：使用授权码和 PKCE 验证码换取令牌。
    /// English: Exchanges an authorization code and PKCE verifier for tokens.
    /// </summary>
    /// <param name="code">中文：Provider 返回的一次性授权码。English: The one-time authorization code returned by the Provider.</param>
    /// <param name="codeVerifier">中文：发起授权请求时生成的 PKCE 验证码。English: The PKCE verifier generated when the authorization request was initiated.</param>
    /// <param name="ct">中文：用于取消 HTTP 请求的令牌。English: A token used to cancel the HTTP request.</param>
    /// <returns>中文：令牌端点返回的访问令牌、刷新令牌、ID Token 和过期时间。English: The access token, refresh token, ID token, and expiration returned by the token endpoint.</returns>
    Task<WorkbenchTokenResult> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct);

    /// <summary>
    /// 中文：使用刷新令牌申请一组新的令牌。
    /// English: Uses a refresh token to request a new token set.
    /// </summary>
    /// <param name="refreshToken">中文：当前有效的刷新令牌。English: The current valid refresh token.</param>
    /// <param name="ct">中文：用于取消 HTTP 请求的令牌。English: A token used to cancel the HTTP request.</param>
    /// <returns>中文：刷新操作返回的新令牌及过期时间。English: The refreshed tokens and their expiration.</returns>
    Task<WorkbenchTokenResult> RefreshTokensAsync(string refreshToken, CancellationToken ct);

    /// <summary>
    /// 中文：通过 Provider 的内省端点验证访问令牌并读取关键声明。
    /// English: Validates an access token through the Provider introspection endpoint and reads key claims.
    /// </summary>
    /// <param name="accessToken">中文：要验证的访问令牌。English: The access token to validate.</param>
    /// <param name="ct">中文：用于取消 HTTP 请求的令牌。English: A token used to cancel the HTTP request.</param>
    /// <returns>中文：令牌是否有效及其主体和客户端标识。English: The token activity status together with its subject and client identifier.</returns>
    Task<AccessTokenIntrospectionResult> IntrospectAccessTokenAsync(string accessToken, CancellationToken ct);
}

/// <summary>
/// 中文：表示授权码交换或刷新操作返回的令牌结果。
/// English: Represents the token result returned by an authorization-code exchange or refresh operation.
/// </summary>
/// <param name="AccessToken">中文：用于访问受保护资源的访问令牌。English: The access token used to access protected resources.</param>
/// <param name="RefreshToken">中文：用于申请新令牌的刷新令牌。English: The refresh token used to request new tokens.</param>
/// <param name="IdToken">中文：包含已认证用户身份信息的 ID Token；刷新时可能为空。English: The ID token containing authenticated-user identity data; it may be absent during refresh.</param>
/// <param name="ExpiresIn">中文：访问令牌的有效秒数。English: The access-token lifetime in seconds.</param>
public sealed record WorkbenchTokenResult(
    string AccessToken,
    string RefreshToken,
    string? IdToken,
    int ExpiresIn);

/// <summary>
/// 中文：表示访问令牌内省操作的结果。
/// English: Represents the result of an access-token introspection operation.
/// </summary>
/// <param name="Active">中文：令牌当前是否有效。English: Whether the token is currently active.</param>
/// <param name="Subject">中文：令牌关联的主体标识；未提供时为空。English: The subject identifier associated with the token, or null when unavailable.</param>
/// <param name="ClientId">中文：令牌关联的客户端标识；未提供时为空。English: The client identifier associated with the token, or null when unavailable.</param>
public sealed record AccessTokenIntrospectionResult(bool Active, string? Subject, string? ClientId);
