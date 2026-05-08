namespace NexusAuth.Application.Clients;

public class ClientMetadataService : IClientMetadataService
{
    public Task<ClientMetadataDto> GetAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new ClientMetadataDto(
            GetIdentityScopes(),
            GetGrantTypes(),
            GetTokenEndpointAuthMethods()));
    }

    private static List<ClientOptionDto> GetIdentityScopes()
    {
        return
        [
            new("openid", "OpenID（openid）", "OIDC 必需作用域，用于请求 ID Token。"),
            new("profile", "用户资料（profile）", "读取用户基础资料声明。"),
            new("email", "邮箱（email）", "读取用户邮箱声明。"),
            new("phone", "手机号（phone）", "读取用户手机号声明。"),
            new("address", "地址（address）", "读取用户地址声明。"),
            new("offline_access", "离线访问（offline_access）", "允许签发 refresh token。"),
        ];
    }

    private static List<ClientOptionDto> GetGrantTypes()
    {
        return
        [
            new("authorization_code", "授权码模式（authorization_code）", "适用于 Web/BFF/OIDC 登录，推荐配合 PKCE。"),
            new("client_credentials", "客户端凭证模式（client_credentials）", "适用于机器到机器调用，不代表具体用户。"),
            new("refresh_token", "刷新令牌（refresh_token）", "使用 refresh token 换取新的 access token。"),
            new("urn:ietf:params:oauth:grant-type:device_code", "设备码模式（device_code）", "适用于无浏览器或输入受限设备。"),
        ];
    }

    private static List<ClientOptionDto> GetTokenEndpointAuthMethods()
    {
        return
        [
            new(OAuthClient.TokenEndpointAuthMethodClientSecretBasic, "Basic 密钥认证（client_secret_basic）", "通过 Authorization Basic 发送 client_id/client_secret。"),
            new(OAuthClient.TokenEndpointAuthMethodClientSecretPost, "表单密钥认证（client_secret_post）", "通过请求表单发送 client_id/client_secret。"),
            new(OAuthClient.TokenEndpointAuthMethodClientSecretJwt, "共享密钥 JWT 认证（client_secret_jwt）", "客户端使用共享密钥签名 JWT，服务端使用已登记共享密钥验签。"),
            new(OAuthClient.TokenEndpointAuthMethodPrivateKeyJwt, "私钥 JWT 认证（private_key_jwt）", "客户端使用私钥签名 JWT，服务端使用已登记 JWKS 验签。"),
        ];
    }
}
