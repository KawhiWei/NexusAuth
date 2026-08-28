using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NexusAuth.Host.Pages.Account;

[Authorize(AuthenticationSchemes = AppWebModule.AuthenticationScheme)]
public sealed class ChangePasswordModel(IUserService userService) : PageModel
{
    private readonly IUserService userService = userService;

    [BindProperty]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmNewPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; private set; }

    public string? StatusMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (NewPassword != ConfirmNewPassword)
        {
            ErrorMessage = "New passwords do not match.";
            return Page();
        }

        var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
            return Challenge(AppWebModule.AuthenticationScheme);

        try
        {
            await userService.ChangePasswordAsync(userId, CurrentPassword, NewPassword, ct);
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmNewPassword = string.Empty;
            StatusMessage = "Password updated. Sign in again in applications that use refresh tokens.";
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = exception.Message;
        }

        return Page();
    }
}
