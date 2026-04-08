using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NexusAuth.Application.Services;

namespace NexusAuth.Host.Pages;

public class ConsentModel : PageModel
{
    private readonly IAuthorizationService _authorizationService;

    public ConsentModel(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    [BindProperty(SupportsGet = true, Name = "client_id")]
    public string ClientId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true, Name = "redirect_uri")]
    public string RedirectUri { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string Scope { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? State { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Nonce { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Prompt { get; set; }

    [BindProperty(SupportsGet = true, Name = "max_age")]
    public int? MaxAge { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Claims { get; set; }

    [BindProperty(SupportsGet = true, Name = "code_challenge")]
    public string? CodeChallenge { get; set; }

    [BindProperty(SupportsGet = true, Name = "code_challenge_method")]
    public string? CodeChallengeMethod { get; set; }

    public IReadOnlyList<string> ScopeItems { get; private set; } = [];

    public IReadOnlyList<string> ClaimItems { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            var returnUrl = Request.Path + Request.QueryString;
            return Redirect($"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(RedirectUri) || string.IsNullOrWhiteSpace(Scope))
        {
            ErrorMessage = "授权请求缺少必要参数。";
            return Page();
        }

        BindDisplayItems();
        return Page();
    }

    public IActionResult OnPostApprove()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            var returnUrl = Request.Path + Request.QueryString;
            return Redirect($"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var authorizeUrl = BuildAuthorizeUrl(RemoveConsentPrompt(Prompt));
        return Redirect(authorizeUrl);
    }

    public IActionResult OnPostDeny()
    {
        var separator = RedirectUri.Contains('?') ? '&' : '?';
        var redirectUrl = $"{RedirectUri}{separator}error=access_denied&error_description={Uri.EscapeDataString("The resource owner denied the request.")}";
        if (!string.IsNullOrWhiteSpace(State))
            redirectUrl += $"&state={Uri.EscapeDataString(State)}";

        return Redirect(redirectUrl);
    }

    private void BindDisplayItems()
    {
        ScopeItems = Scope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        ClaimItems = ExtractClaimItems(Claims);
    }

    private IReadOnlyList<string> ExtractClaimItems(string? claimsJson)
    {
        if (string.IsNullOrWhiteSpace(claimsJson))
            return [];

        var requestedClaims = _authorizationService.ParseRequestedClaims(claimsJson);
        return requestedClaims.IdTokenClaimRequests
            .Concat(requestedClaims.UserInfoClaimRequests)
            .Select(kvp => BuildClaimDisplayText(kvp.Key, kvp.Value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string BuildClaimDisplayText(string claimName, OidcClaimRequest request)
    {
        var tags = new List<string>();
        if (request.Essential == true)
            tags.Add("essential");
        if (!string.IsNullOrWhiteSpace(request.Value))
            tags.Add($"value={request.Value}");
        if (request.Values.Count > 0)
            tags.Add($"values={string.Join('|', request.Values)}");

        return tags.Count == 0 ? claimName : $"{claimName} ({string.Join(", ", tags)})";
    }

    private string BuildAuthorizeUrl(string? prompt)
    {
        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["scope"] = Scope,
            ["state"] = State,
            ["nonce"] = Nonce,
            ["prompt"] = prompt,
            ["max_age"] = MaxAge?.ToString(),
            ["claims"] = Claims,
            ["code_challenge"] = CodeChallenge,
            ["code_challenge_method"] = CodeChallengeMethod,
        };

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/connect/authorize", query!);
    }

    private static string? RemoveConsentPrompt(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return null;

        var values = prompt
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.Equals(value, "consent", StringComparison.Ordinal))
            .ToArray();

        return values.Length == 0 ? null : string.Join(' ', values);
    }
}
