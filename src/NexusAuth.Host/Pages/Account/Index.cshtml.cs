using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using NexusAuth.Application.Services.Tokens;

namespace NexusAuth.Host.Pages.Account;

[Authorize(AuthenticationSchemes = AppWebModule.AuthenticationScheme)]
public sealed class IndexModel(ISsoSessionService sessionService, ITokenService tokenService) : PageModel
{
    public string DisplayName => User.Identity?.Name ?? "NexusAuth 用户";

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            && Guid.TryParse(User.FindFirstValue("sid"), out var sessionId))
        {
            await tokenService.RevokeAllUserTokensAsync(userId, HttpContext.RequestAborted);
            await sessionService.RevokeAllForUserAsync(userId, HttpContext.RequestAborted);
        }

        await HttpContext.SignOutAsync(AppWebModule.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }
}
