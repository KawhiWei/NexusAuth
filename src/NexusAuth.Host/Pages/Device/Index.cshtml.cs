using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NexusAuth.Application.Services;

namespace NexusAuth.Host.Pages.Device;

public class IndexModel : PageModel
{
    private readonly IDeviceAuthorizationService _deviceAuthorizationService;

    public IndexModel(IDeviceAuthorizationService deviceAuthorizationService)
    {
        _deviceAuthorizationService = deviceAuthorizationService;
    }

    [BindProperty(SupportsGet = true, Name = "user_code")]
    public string UserCode { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? StatusMessage { get; set; }

    public DeviceAuthorizationSessionResult? Session { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(UserCode))
            return Page();

        await LoadSessionAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LoadSessionAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Redirect($"/account/login?returnUrl={Uri.EscapeDataString($"/device?user_code={UserCode}")}");

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            ErrorMessage = "Unable to determine current user.";
            await LoadSessionAsync(ct);
            return Page();
        }

        Session = await _deviceAuthorizationService.ApproveAsync(UserCode, userId, ct);
        if (!Session.IsSuccess)
            ErrorMessage = Session.Error;
        else
            StatusMessage = "Device approved. You can return to your device.";

        return Page();
    }

    public async Task<IActionResult> OnPostDenyAsync(CancellationToken ct)
    {
        Session = await _deviceAuthorizationService.DenyAsync(UserCode, ct);
        if (!Session.IsSuccess)
            ErrorMessage = Session.Error;
        else
            StatusMessage = "Device request denied.";

        return Page();
    }

    private async Task LoadSessionAsync(CancellationToken ct)
    {
        Session = await _deviceAuthorizationService.GetByUserCodeAsync(UserCode, ct);
        if (!Session.IsSuccess)
        {
            ErrorMessage = Session.Error;
            Session = null;
        }
    }
}
