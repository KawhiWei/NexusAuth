using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace NexusAuth.Host;

public sealed class SsoCookieAuthenticationEvents(
    ISsoSessionService sessionService,
    IUserService userService) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var subject = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var session = context.Principal?.FindFirst("sid")?.Value;
        if (!Guid.TryParse(subject, out var userId)
            || !Guid.TryParse(session, out var sessionId)
            || !await sessionService.IsActiveAsync(sessionId, userId, context.HttpContext.RequestAborted))
        {
            await RejectAsync(context);
            return;
        }

        var user = await userService.FindByIdAsync(userId, context.HttpContext.RequestAborted);
        if (user is null || !user.IsActive)
            await RejectAsync(context);
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(AppWebModule.AuthenticationScheme);
    }
}
