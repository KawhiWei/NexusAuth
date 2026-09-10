using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NexusAuth.Domain.AggregateRoots.Users;

namespace NexusAuth.Host.Authentication;

/// <summary>
/// 将已经验证成功的用户转换为 NexusAuth SSO Session 和 Cookie Principal。
/// 本服务不校验密码或 Passkey，只接收上游已经确认的认证结果。
/// </summary>
public sealed class WebSignInService(
    ISsoSessionService sessionService,
    IOptions<LoginFlowOptions> flowOptions) : IWebSignInService
{
    private const string AuthTimeClaimType = "auth_time";
    private const string AmrClaimType = "amr";
    private const string AcrClaimType = "acr";
    private readonly LoginFlowOptions _flowOptions = flowOptions.Value;

    public async Task SignInAsync(
        HttpContext httpContext,
        User user,
        bool rememberMe,
        DateTimeOffset authenticatedAt,
        string authenticationMethods,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationMethods);

        var issuedAt = DateTimeOffset.UtcNow;
        var authenticationProperties = _flowOptions.CreateAuthenticationProperties(rememberMe, issuedAt);
        var sessionId = await sessionService.CreateAsync(
            user.Id,
            authenticationProperties.ExpiresUtc!.Value - issuedAt,
            ct);

        // amr 表示实际认证方式；acr 提供给 OIDC 客户端判断认证强度。
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("sid", sessionId.ToString()),
            new(AuthTimeClaimType, authenticatedAt.ToUnixTimeSeconds().ToString()),
            new(AmrClaimType, authenticationMethods),
            new(AcrClaimType, ResolveAuthenticationContext(authenticationMethods)),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AppWebModule.AuthenticationScheme));

        await httpContext.SignInAsync(
            AppWebModule.AuthenticationScheme,
            principal,
            authenticationProperties);
    }

    private static string ResolveAuthenticationContext(string authenticationMethods) =>
        authenticationMethods.Contains("webauthn", StringComparison.Ordinal)
            ? "urn:nexusauth:acr:webauthn-uv"
            : authenticationMethods.Contains("otp", StringComparison.Ordinal)
                ? "urn:nexusauth:acr:mfa"
                : "urn:nexusauth:acr:pwd";
}
