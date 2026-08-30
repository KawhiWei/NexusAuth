using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NexusAuth.Host.Pages;

[AllowAnonymous]
public sealed class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        return User.Identity?.IsAuthenticated == true
            ? Redirect("/account")
            : Redirect("/account/login");
    }
}
