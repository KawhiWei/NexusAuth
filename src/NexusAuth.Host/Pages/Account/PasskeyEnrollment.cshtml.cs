using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using NexusAuth.Application.Services.WebAuthn;
using NexusAuth.Host.Authentication;
using System.Security.Claims;

namespace NexusAuth.Host.Pages.Account;

[Authorize(AuthenticationSchemes = AppWebModule.AuthenticationScheme)]
public sealed class PasskeyEnrollmentModel(
    WebAuthnEnrollmentStateProtector enrollmentStateProtector,
    Fido2 fido2,
    IUserService userService,
    IWebAuthnService webAuthnService,
    IOptions<WebAuthnOptions> webAuthnOptions) : PageModel
{
    private readonly WebAuthnOptions _webAuthnOptions = webAuthnOptions.Value;

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    public string SkipUrl { get; private set; } = "/account";

    public IActionResult OnGet()
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        if (!enrollmentStateProtector.TryUnprotect(Token, out var enrollment) || enrollment is null)
            return RedirectToPage("/Account/Register");
        if (!IsCurrentUser(enrollment.UserId))
            return Forbid(AppWebModule.AuthenticationScheme);

        SkipUrl = GetPostRegistrationUrl(enrollment.ReturnUrl);
        if (!_webAuthnOptions.Enabled)
            return Redirect(SkipUrl);

        return Page();
    }

    public async Task<IActionResult> OnPostOptionsAsync(
        [FromBody] PasskeyRegistrationOptionsRequest request,
        CancellationToken ct)
    {
        if (!_webAuthnOptions.Enabled)
            return NotFound();

        if (!enrollmentStateProtector.TryUnprotect(request.EnrollmentToken, out var enrollment) || enrollment is null)
            return BadRequest(new { error = "invalid_enrollment", error_description = "The passkey enrollment has expired. Register again." });
        if (!IsCurrentUser(enrollment.UserId))
            return Forbid(AppWebModule.AuthenticationScheme);

        var user = await userService.FindByIdAsync(enrollment.UserId, ct);
        if (user is null || !user.IsActive)
            return BadRequest(new { error = "invalid_enrollment", error_description = "The account is no longer available." });

        var existingCredentials = await webAuthnService.GetEnabledCredentialsAsync(user.Id, ct);
        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User { Id = user.Id.ToByteArray(), Name = user.Username, DisplayName = user.Nickname },
            ExcludeCredentials = existingCredentials.Select(credential => new PublicKeyCredentialDescriptor(credential.CredentialId)).ToArray(),
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = _webAuthnOptions.UserVerification,
            },
            AttestationPreference = AttestationConveyancePreference.None,
        });
        var flowToken = await webAuthnService.CreateRegistrationChallengeAsync(
            user.Id,
            JsonSerializer.Serialize(options),
            enrollment.ReturnUrl,
            DateTimeOffset.UtcNow.AddSeconds(_webAuthnOptions.ChallengeLifetimeSeconds),
            ct);

        return new JsonResult(new { flowToken, publicKey = options });
    }

    public async Task<IActionResult> OnPostVerifyAsync(
        [FromBody] PasskeyRegistrationVerificationRequest request,
        CancellationToken ct)
    {
        if (!_webAuthnOptions.Enabled)
            return NotFound();

        var state = await webAuthnService.ConsumeRegistrationChallengeAsync(
            request.FlowToken,
            DateTimeOffset.UtcNow,
            ct);
        if (state is null || !state.UserId.HasValue)
            return BadRequest(new { error = "invalid_challenge", error_description = "The passkey request expired or was already used." });
        if (!IsCurrentUser(state.UserId.Value))
            return Forbid(AppWebModule.AuthenticationScheme);

        var options = JsonSerializer.Deserialize<CredentialCreateOptions>(state.OptionsJson);
        if (options is null)
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "server_error" });

        try
        {
            var result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = request.Credential,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = async (input, cancellationToken) =>
                    await webAuthnService.FindCredentialAsync(input.CredentialId, cancellationToken) is null,
            }, ct);
            await webAuthnService.RegisterCredentialAsync(new WebAuthnCredentialRegistration(
                state.UserId.Value,
                result.Id,
                result.PublicKey,
                result.SignCount,
                result.AaGuid,
                result.Transports.Select(value => value.ToString()).ToArray(),
                result.IsBackupEligible,
                result.IsBackedUp,
                request.DisplayName), ct);
        }
        catch (Fido2VerificationException exception)
        {
            return BadRequest(new { error = "invalid_credential", error_description = exception.Message });
        }

        var redirectUrl = !string.IsNullOrWhiteSpace(state.ReturnUrl) && Url.IsLocalUrl(state.ReturnUrl)
            ? state.ReturnUrl
            : "/account?passkeyRegistered=1";
        return new JsonResult(new { redirectUrl });
    }

    private string GetPostRegistrationUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : "/account";

    private bool IsCurrentUser(Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)
        && currentUserId == userId;
}
