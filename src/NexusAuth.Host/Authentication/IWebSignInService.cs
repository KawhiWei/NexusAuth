using NexusAuth.Domain.AggregateRoots.Users;

namespace NexusAuth.Host.Authentication;

/// <summary>
/// 统一完成 Web 登录会话创建和认证 Cookie 签发，供密码、TOTP、Passkey 与注册流程复用。
/// </summary>
public interface IWebSignInService
{
    Task SignInAsync(
        HttpContext httpContext,
        User user,
        bool rememberMe,
        DateTimeOffset authenticatedAt,
        string authenticationMethods,
        CancellationToken ct = default);
}
