using System.Text;

namespace NexusAuth.Application.Services;

public static class OAuthClientAuthenticationParser
{
    /// <summary>
    /// 统一解析 OAuth2 客户端认证输入，优先兼容 client_secret_basic，
    /// 如果请求头不存在或格式错误，则回退到表单中的 client_id / client_secret。
    /// 主要调用方：Host 层的 token、introspect、revocation、device authorization 端点。
    /// </summary>
    public static (string? ClientId, string? ClientSecret) ResolveClientAuthentication(
        string? authorizationHeader,
        string? formClientId,
        string? formClientSecret)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader)
            || !authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return (formClientId, formClientSecret);
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorizationHeader[6..]));
            var separatorIndex = decoded.IndexOf(':');
            if (separatorIndex < 0)
                return (formClientId, formClientSecret);

            var headerClientId = decoded[..separatorIndex];
            var headerClientSecret = decoded[(separatorIndex + 1)..];
            return (
                string.IsNullOrWhiteSpace(headerClientId) ? formClientId : headerClientId,
                string.IsNullOrWhiteSpace(headerClientSecret) ? formClientSecret : headerClientSecret);
        }
        catch (FormatException)
        {
            return (formClientId, formClientSecret);
        }
    }
}
