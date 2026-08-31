using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using NexusAuth.Application.Users;
using NexusAuth.Domain.AggregateRoots.Users;
using NexusAuth.Host.Authentication;

namespace NexusAuth.Host.Pages.Account;

[Authorize(AuthenticationSchemes = AppWebModule.AuthenticationScheme)]
public sealed class TwoFactorModel(
    ITotpService totpService,
    IUserService userService,
    ISsoSessionService sessionService,
    IOptions<LoginFlowOptions> flowOptions) : PageModel
{
    private readonly LoginFlowOptions _flowOptions = flowOptions.Value;

    [BindProperty]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    public string Code { get; set; } = string.Empty;

    [BindProperty]
    public string EnrollmentSecret { get; set; } = string.Empty;

    [BindProperty]
    public string ProtectedEnrollmentSecret { get; set; } = string.Empty;

    [BindProperty]
    public string OtpAuthUri { get; set; } = string.Empty;

    [BindProperty]
    public Guid CredentialId { get; set; }

    public bool IsEnabled { get; private set; }

    public bool IsEnrollmentPending { get; private set; }

    public IReadOnlyList<TotpCredentialSummary> Credentials { get; private set; } = [];

    public bool CanDisable => !string.Equals(
        _flowOptions.FindStep(LoginFlowStepTypes.Totp)?.Requirement,
        LoginFlowRequirements.Required,
        StringComparison.OrdinalIgnoreCase);

    public string? ErrorMessage { get; private set; }

    public string? StatusMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Challenge(AppWebModule.AuthenticationScheme);

        await LoadCredentialsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostBeginAsync(CancellationToken ct)
    {
        var user = await ValidateCurrentPasswordAsync(ct);
        if (user is null)
        {
            ErrorMessage = "Current password is incorrect.";
            await LoadCredentialsAsync(ct);
            return Page();
        }

        var enrollment = await totpService.BeginEnrollmentAsync(user.Id, ct);
        EnrollmentSecret = enrollment.ManualEntryKey;
        ProtectedEnrollmentSecret = enrollment.ProtectedSecret;
        OtpAuthUri = enrollment.OtpauthUri;
        CurrentPassword = string.Empty;
        IsEnrollmentPending = true;
        await LoadCredentialsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Challenge(AppWebModule.AuthenticationScheme);

        IsEnrollmentPending = true;
        await LoadCredentialsAsync(ct);
        if (!await totpService.ConfirmEnrollmentAsync(userId, ProtectedEnrollmentSecret, Code, ct))
        {
            Code = string.Empty;
            ErrorMessage = "The code is invalid or the enrollment has expired. Start over if necessary.";
            return Page();
        }

        EnrollmentSecret = string.Empty;
        ProtectedEnrollmentSecret = string.Empty;
        OtpAuthUri = string.Empty;
        IsEnrollmentPending = false;
        await LoadCredentialsAsync(ct);
        StatusMessage = "Authenticator verification is enabled.";
        return Page();
    }

    public async Task<IActionResult> OnPostDisableAsync(CancellationToken ct)
    {
        if (!CanDisable)
        {
            ErrorMessage = "The active login flow requires TOTP and does not allow it to be disabled.";
            await LoadCredentialsAsync(ct);
            return Page();
        }

        var user = await ValidateCurrentPasswordAsync(ct);
        if (user is null)
        {
            ErrorMessage = "Current password is incorrect.";
            await LoadCredentialsAsync(ct);
            return Page();
        }

        if (!await totpService.DisableAsync(user.Id, CredentialId, ct))
        {
            ErrorMessage = "The authenticator is no longer available.";
            await LoadCredentialsAsync(ct);
            return Page();
        }
        await sessionService.RevokeAllForUserAsync(user.Id, ct);
        await HttpContext.SignOutAsync(AppWebModule.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }

    private async Task<User?> ValidateCurrentPasswordAsync(CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(CurrentPassword))
            return null;

        return await userService.ValidateCredentialsAsync(username, CurrentPassword, ct);
    }

    private async Task LoadCredentialsAsync(CancellationToken ct)
    {
        if (TryGetUserId(out var userId))
        {
            Credentials = await totpService.GetCredentialsAsync(userId, ct);
            IsEnabled = Credentials.Any(credential => credential.IsEnabled);
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
