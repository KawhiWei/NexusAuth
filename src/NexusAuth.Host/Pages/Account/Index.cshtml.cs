using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace NexusAuth.Host.Pages.Account;

[Authorize(AuthenticationSchemes = AppWebModule.AuthenticationScheme)]
public sealed class IndexModel(ISsoSessionService sessionService) : PageModel
{
    public string DisplayName => User.Identity?.Name ?? "NexusAuth 用户";

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            && Guid.TryParse(User.FindFirstValue("sid"), out var sessionId))
        {
            await sessionService.RevokeAsync(sessionId, userId, HttpContext.RequestAborted);
        }

        await HttpContext.SignOutAsync(AppWebModule.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }
}
