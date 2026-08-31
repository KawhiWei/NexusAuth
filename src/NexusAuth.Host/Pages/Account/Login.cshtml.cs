using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using NexusAuth.Application.Services;
using NexusAuth.Application.Services.LoginAudits;
using NexusAuth.Application.Services.Security;
using NexusAuth.Application.Users;
using NexusAuth.Host.Authentication;
using NexusAuth.Domain.AggregateRoots.Users;

namespace NexusAuth.Host.Pages.Account;

public class LoginModel(
    IUserService userService,
    ISsoSessionService sessionService,
    ILoginAuditService loginAuditService,
    ISecurityPolicyService securityPolicyService,
    ITotpService totpService,
    LoginFlowStateProtector flowStateProtector,
    IOptions<LoginFlowOptions> flowOptions) : PageModel
{
    private const string AuthTimeClaimType = "auth_time";
    private const string AmrClaimType = "amr";
    private const string AcrClaimType = "acr";

    private readonly LoginFlowOptions _flowOptions = flowOptions.Value;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberMe { get; set; }

    [BindProperty]
    public string TotpCode { get; set; } = string.Empty;

    [BindProperty]
    public string FlowToken { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? ClientName { get; set; }

    public bool RequiresTotp { get; private set; }

    public bool ShowRememberMe => _flowOptions.AllowRememberMe;

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

            return Redirect("/account");
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
            await RecordLoginAsync(null, false, "MissingCredentials");
            ErrorMessage = "Username and password are required.";
            await TryExtractClientNameAsync();
            return Page();
        }

        var user = await userService.ValidateCredentialsAsync(Username, Password);
        if (user is null)
        {
            await RecordLoginAsync(null, false, "InvalidCredentials");
            ErrorMessage = "Invalid username or password.";
            await TryExtractClientNameAsync();
            return Page();
        }

        var userPolicy = securityPolicyService.CheckUser(user);
        if (!userPolicy.IsSuccess)
        {
            await RecordLoginAsync(user.Id, false, "UserDeniedByPolicy");
            ErrorMessage = "Invalid username or password.";
            await TryExtractClientNameAsync();
            return Page();
        }

        var totpStep = _flowOptions.FindStep(LoginFlowStepTypes.Totp);
        if (totpStep is not null)
        {
            var totpEnabled = await totpService.IsEnabledAsync(user.Id, HttpContext.RequestAborted);
            var totpRequired = string.Equals(
                totpStep.Requirement,
                LoginFlowRequirements.Required,
                StringComparison.OrdinalIgnoreCase);
            if (totpRequired && !totpEnabled)
            {
                await RecordLoginAsync(user.Id, false, "TotpEnrollmentRequired");
                ErrorMessage = "This account must configure an authenticator before it can sign in.";
                await TryExtractClientNameAsync();
                return Page();
            }

            if (totpEnabled)
            {
                RequiresTotp = true;
                FlowToken = flowStateProtector.Protect(
                    new PendingLoginFlowState(
                        user.Id,
                        user.Username,
                        ReturnUrl,
                        _flowOptions.AllowRememberMe && RememberMe,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    TimeSpan.FromMinutes(_flowOptions.PendingStateLifetimeMinutes));
                Password = string.Empty;
                await TryExtractClientNameAsync();
                return Page();
            }
        }

        return await CompleteLoginAsync(user, RememberMe, DateTimeOffset.UtcNow, "pwd");
    }

    public async Task<IActionResult> OnPostTotpAsync()
    {
        RequiresTotp = true;
        if (!flowStateProtector.TryUnprotect(FlowToken, out var flowState) || flowState is null)
        {
            RequiresTotp = false;
            FlowToken = string.Empty;
            ErrorMessage = "The login attempt expired. Start again.";
            return Page();
        }

        Username = flowState.Username;
        ReturnUrl = flowState.ReturnUrl;
        RememberMe = flowState.RememberMe;
        await TryExtractClientNameAsync();

        if (string.IsNullOrWhiteSpace(TotpCode))
        {
            ErrorMessage = "Enter the six-digit authenticator code.";
            return Page();
        }

        var user = await userService.FindByIdAsync(flowState.UserId, HttpContext.RequestAborted);
        if (user is null || !user.IsActive || !securityPolicyService.CheckUser(user).IsSuccess)
        {
            await RecordLoginAsync(flowState.UserId, false, "UserUnavailable");
            RequiresTotp = false;
            FlowToken = string.Empty;
            ErrorMessage = "The login attempt is no longer valid. Start again.";
            return Page();
        }

        if (!await totpService.ValidateAsync(user.Id, TotpCode, HttpContext.RequestAborted))
        {
            await RecordLoginAsync(user.Id, false, "InvalidTotp");
            TotpCode = string.Empty;
            ErrorMessage = "Invalid or already used authenticator code.";
            return Page();
        }

        var authenticatedAt = DateTimeOffset.FromUnixTimeSeconds(flowState.AuthenticatedAtUnixSeconds);
        return await CompleteLoginAsync(user, flowState.RememberMe, authenticatedAt, "pwd otp");
    }

    private async Task<IActionResult> CompleteLoginAsync(
        User user,
        bool rememberMe,
        DateTimeOffset authenticatedAt,
        string authenticationMethods)
    {
        var sessionId = await sessionService.CreateAsync(user.Id, HttpContext.RequestAborted);
        Username = user.Username;
        await RecordLoginAsync(user.Id, true, null);

        // Build claims and sign in
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("sid", sessionId.ToString()),
            // 中文注释：记录认证时间与认证方式，供 OIDC 的 max_age、auth_time、amr、acr 扩展使用。
            new(AuthTimeClaimType, authenticatedAt.ToUnixTimeSeconds().ToString()),
            new(AmrClaimType, authenticationMethods),
            new(AcrClaimType, authenticationMethods.Contains("otp", StringComparison.Ordinal)
                ? "urn:nexusauth:acr:mfa"
                : "urn:nexusauth:acr:pwd"),
        };

        var identity = new ClaimsIdentity(claims, AppWebModule.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = _flowOptions.AllowRememberMe && rememberMe,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(_flowOptions.SessionLifetimeMinutes),
        };

        await HttpContext.SignInAsync(
            AppWebModule.AuthenticationScheme,
            principal,
            authProperties);

        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            return Redirect(ReturnUrl);

        return Redirect("/account");
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

    private Task RecordLoginAsync(Guid? userId, bool isSuccessful, string? failureReason)
    {
        var clientId = TryExtractClientId();
        return loginAuditService.RecordAsync(new LoginAuditRecord(
            Username,
            userId,
            clientId,
            isSuccessful,
            failureReason,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Request.Headers.UserAgent.ToString()), HttpContext.RequestAborted);
    }

    private string? TryExtractClientId()
    {
        if (string.IsNullOrWhiteSpace(ReturnUrl))
            return null;

        try
        {
            var uri = new Uri(new Uri("http://localhost"), ReturnUrl);
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            return query.TryGetValue("client_id", out var clientId) ? clientId.ToString() : null;
        }
        catch (UriFormatException)
        {
            return null;
        }
    }
}
