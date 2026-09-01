using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NexusAuth.Host.Pages.Account;

[Authorize(AuthenticationSchemes = AppWebModule.AuthenticationScheme)]
public sealed class IndexModel : PageModel
{
    public string DisplayName => User.Identity?.Name ?? "NexusAuth 用户";

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(AppWebModule.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }
}
