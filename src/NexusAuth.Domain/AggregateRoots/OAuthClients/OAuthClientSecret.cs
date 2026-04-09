namespace NexusAuth.Domain.AggregateRoots.OAuthClients;

public sealed record OAuthClientSecret(string Type, string Value, string? Description = null)
{
    public const string TypeSharedSecret = "shared_secret";

    public const string TypeJwks = "jwks";

    /// <summary>
    /// 创建共享密钥类型的客户端凭据。
    /// 主要调用方：client_secret_basic / client_secret_post 客户端初始化。
    /// </summary>
    public static OAuthClientSecret CreateSharedSecret(string rawSecret, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawSecret);
        return new OAuthClientSecret(TypeSharedSecret, BCrypt.Net.BCrypt.HashPassword(rawSecret, workFactor: 12), description);
    }

    /// <summary>
    /// 创建 JWKS 类型的客户端凭据。
    /// 主要调用方：private_key_jwt 客户端注册，服务端据此验签 client_assertion。
    /// </summary>
    public static OAuthClientSecret CreateJwks(string jwksJson, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jwksJson);
        return new OAuthClientSecret(TypeJwks, jwksJson, description);
    }

    public bool VerifySharedSecret(string rawSecret)
    {
        if (!string.Equals(Type, TypeSharedSecret, StringComparison.Ordinal))
            return false;

        return BCrypt.Net.BCrypt.Verify(rawSecret, Value);
    }
}
