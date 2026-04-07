using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NexusAuth.Application.Services;

namespace NexusAuth.Host.Pages.Account;

public class LoginModel : PageModel
{
    private const string AuthTimeClaimType = "auth_time";
    private const string AmrClaimType = "amr";
    private const string AcrClaimType = "acr";

    private readonly IUserService _userService;
    private readonly IClientService _clientService;

    public LoginModel(IUserService userService, IClientService clientService)
    {
        _userService = userService;
        _clientService = clientService;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? ClientName { get; set; }

    /// <summary>
    /// 渲染登录页，并清理已有外部认证 Cookie。
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        // If user is already authenticated, redirect back immediately
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                return Redirect(ReturnUrl);

            return Redirect("/");
        }

        // Try to extract client_id from returnUrl to show client name
        await TryExtractClientNameAsync();

        return Page();
    }

    /// <summary>
    /// 提交登录表单，校验账号密码并建立 Cookie 登录会话。
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Username and password are required.";
            await TryExtractClientNameAsync();
            return Page();
        }

        var user = await _userService.ValidateCredentialsAsync(Username, Password);
        if (user is null)
        {
            ErrorMessage = "Invalid username or password.";
            await TryExtractClientNameAsync();
            return Page();
        }

        // Build claims and sign in
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            // 中文注释：记录认证时间与认证方式，供 OIDC 的 max_age、auth_time、amr、acr 扩展使用。
            new(AuthTimeClaimType, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new(AmrClaimType, "pwd"),
            new(AcrClaimType, "urn:nexusauth:acr:pwd"),
        };

        var identity = new ClaimsIdentity(claims, AppWebModule.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24),
        };

        await HttpContext.SignInAsync(
            AppWebModule.AuthenticationScheme,
            principal,
            authProperties);

        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            return Redirect(ReturnUrl);

        return Redirect("/");
    }

    private async Task TryExtractClientNameAsync()
    {
        if (string.IsNullOrWhiteSpace(ReturnUrl))
            return;

        try
        {
            // Parse returnUrl to extract client_id query parameter
            var uri = new Uri(ReturnUrl, UriKind.RelativeOrAbsolute);
            string? query;

            if (uri.IsAbsoluteUri)
                query = uri.Query;
            else
            {
                // For relative URIs, prepend a dummy base to parse query string
                var absolute = new Uri(new Uri("http://localhost"), ReturnUrl);
                query = absolute.Query;
            }

            if (string.IsNullOrEmpty(query))
                return;

            var queryParams = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query);
            if (queryParams.TryGetValue("client_id", out var clientId) && !string.IsNullOrWhiteSpace(clientId))
            {
                ClientName = clientId;
            }
        }
        catch
        {
            // Ignore parsing errors — client name is cosmetic
        }
    }
}
