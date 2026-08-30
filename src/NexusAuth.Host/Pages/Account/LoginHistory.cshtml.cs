using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NexusAuth.Application.Services.LoginAudits;

namespace NexusAuth.Host.Pages.Account;

[Authorize(AuthenticationSchemes = AppWebModule.AuthenticationScheme)]
public sealed class LoginHistoryModel(ILoginAuditService loginAuditService) : PageModel
{
    public IReadOnlyList<LoginAuditLogDto> LoginAudits { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
            return Challenge(AppWebModule.AuthenticationScheme);

        LoginAudits = await loginAuditService.GetRecentForUserAsync(userId, 20, ct);
        return Page();
    }
}
