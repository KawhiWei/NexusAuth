using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using NexusAuth.Host.Authentication;

namespace NexusAuth.Host.Pages.Account;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
public sealed class RegisterModel(
    IUserService userService,
    IAntiforgery antiforgery,
    IOptions<SelfRegistrationOptions> selfRegistrationOptions) : PageModel
{
    private readonly SelfRegistrationOptions selfRegistration = selfRegistrationOptions.Value;

    [BindProperty]
    [Required(ErrorMessage = "请输入登录账号。")]
    [StringLength(64, MinimumLength = 3, ErrorMessage = "登录账号长度应为 3 到 64 个字符。")]
    [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "登录账号只能包含字母、数字、点、下划线和连字符。")]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "请输入用户名。")]
    [StringLength(64, MinimumLength = 2, ErrorMessage = "用户名长度应为 2 到 64 个字符。")]
    public string Nickname { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "请输入邮箱地址。")]
    [EmailAddress(ErrorMessage = "请输入有效的邮箱地址。")]
    [StringLength(256, ErrorMessage = "邮箱地址不能超过 256 个字符。")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "请输入密码。")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "密码长度至少为 8 个字符。")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "请再次输入密码。")]
    [Compare(nameof(Password), ErrorMessage = "两次输入的密码不一致。")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public bool SelfRegistrationEnabled => selfRegistration.Enabled;

    public IActionResult OnGet()
    {
        if (!SelfRegistrationEnabled)
            return NotFound();

        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/account");

        ReturnUrl = GetLocalReturnUrl(ReturnUrl);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!SelfRegistrationEnabled)
            return NotFound();

        ReturnUrl = GetLocalReturnUrl(ReturnUrl);

        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            ClearPasswords();
            return Page();
        }

        try
        {
            await userService.RegisterAsync(
                Username.Trim(),
                Password,
                Nickname.Trim(),
                Email.Trim(),
                ct: ct);

            return RedirectToPage("/Account/Login", new { ReturnUrl, registered = "1" });
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = exception.Message;
        }

        ClearPasswords();
        return Page();
    }

    private string? GetLocalReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
    }

    private void ClearPasswords()
    {
        var passwordErrors = ModelState[nameof(Password)]?.Errors.Select(error => error.ErrorMessage).ToArray() ?? [];
        var confirmationErrors = ModelState[nameof(ConfirmPassword)]?.Errors.Select(error => error.ErrorMessage).ToArray() ?? [];
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        ModelState.Remove(nameof(Password));
        ModelState.Remove(nameof(ConfirmPassword));

        foreach (var error in passwordErrors)
            ModelState.AddModelError(nameof(Password), error);

        foreach (var error in confirmationErrors)
            ModelState.AddModelError(nameof(ConfirmPassword), error);
    }
}
