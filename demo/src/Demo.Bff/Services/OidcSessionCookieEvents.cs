using Demo.Bff.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Demo.Bff.Services;

/// <summary>
/// 统一处理 BFF Cookie 会话的有效性检查与自动续签。
/// 主要调用方：ASP.NET Core Cookie 中间件，在每次请求验证 Principal 时自动触发。
/// </summary>
public sealed class OidcSessionCookieEvents : CookieAuthenticationEvents
{
    private readonly OidcBffService _oidcBffService;

    public OidcSessionCookieEvents(OidcBffService oidcBffService)
    {
        _oidcBffService = oidcBffService;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var result = await _oidcBffService.EnsureActiveSessionAsync(context.Principal, context.HttpContext.RequestAborted);
        if (result is null)
        {
            context.RejectPrincipal();
            return;
        }

        if (result.IsRefreshed)
        {
            context.ReplacePrincipal(result.Principal);
            context.ShouldRenew = true;
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
