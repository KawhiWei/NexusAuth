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

    /// <summary>
    /// 访问设备授权页，按 user_code 拉取会话状态。
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(UserCode))
            return Page();

        await LoadSessionAsync(ct);
        return Page();
    }

    /// <summary>
    /// 提交 user_code 并查询设备授权状态。
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LoadSessionAsync(ct);
        return Page();
    }

    /// <summary>
    /// 当前登录用户确认设备授权。
    /// </summary>
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

    /// <summary>
    /// 当前用户拒绝设备授权请求。
    /// </summary>
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
