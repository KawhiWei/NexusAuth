using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NexusAuth.Application.Clients;
using NexusAuth.Application.Services;

namespace NexusAuth.Host.Pages;

public class ConsentModel(
    IAuthorizationService authorizationService,
    IClientService clientService) : PageModel
{
    private readonly IAuthorizationService _authorizationService = authorizationService;
    private readonly IClientService _clientService = clientService;

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

    [BindProperty(SupportsGet = true, Name = "response_mode")]
    public string? ResponseMode { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Claims { get; set; }

    [BindProperty(SupportsGet = true, Name = "code_challenge")]
    public string? CodeChallenge { get; set; }

    [BindProperty(SupportsGet = true, Name = "code_challenge_method")]
    public string? CodeChallengeMethod { get; set; }

    public IReadOnlyList<string> ScopeItems { get; private set; } = [];

    public IReadOnlyList<string> ClaimItems { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
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

        var validation = await ValidateAuthorizeRequestAsync(ct);
        if (!validation.IsSuccess)
        {
            ErrorMessage = validation.Error;
            return Page();
        }

        try
        {
            BindDisplayItems();
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            var returnUrl = Request.Path + Request.QueryString;
            return Redirect($"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var validation = await ValidateAuthorizeRequestAsync(ct);
        if (!validation.IsSuccess)
        {
            ErrorMessage = validation.Error;
            return Page();
        }

        var authorizeUrl = BuildAuthorizeUrl(RemoveConsentPrompt(Prompt));
        return Redirect(authorizeUrl);
    }

    public async Task<IActionResult> OnPostDenyAsync(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            var returnUrl = Request.Path + Request.QueryString;
            return Redirect($"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var validation = await ValidateAuthorizeRequestAsync(ct);
        if (!validation.IsSuccess)
        {
            ErrorMessage = validation.Error;
            return Page();
        }

        var responseMode = ResolveResponseMode(ResponseMode) ?? "query";
        return BuildAuthorizationResponse(
            RedirectUri,
            responseMode,
            new Dictionary<string, string?>
            {
                ["error"] = "access_denied",
                ["error_description"] = "The resource owner denied the request.",
                ["state"] = State,
            });
    }

    private async Task<ClientValidationResult> ValidateAuthorizeRequestAsync(CancellationToken ct)
    {
        if (ResolveResponseMode(ResponseMode) is null)
        {
            return ClientValidationResult.Failure(
                "invalid_request",
                "response_mode must be query or form_post.",
                redirectUriValidated: true);
        }

        if (MaxAge is < 0)
        {
            return ClientValidationResult.Failure(
                "invalid_request",
                "max_age must not be negative.",
                redirectUriValidated: true);
        }

        var clientValidation = await _clientService.ValidateClientForAuthorizationAsync(
            ClientId,
            RedirectUri,
            "authorization_code",
            CodeChallenge,
            CodeChallengeMethod,
            ct);
        if (!clientValidation.IsSuccess)
            return clientValidation;

        var scopeValidation = await _clientService.ValidateScopesAsync(
            ClientId,
            Scope,
            allowIdentityScopes: true,
            ct);
        if (!scopeValidation.IsSuccess)
            return ClientValidationResult.Failure(
                scopeValidation.ErrorCode ?? "invalid_scope",
                scopeValidation.Error ?? "The requested scope is invalid.",
                redirectUriValidated: true);

        return ClientValidationResult.Success();
    }

    private void BindDisplayItems()
    {
        ScopeItems = [.. Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        ClaimItems = ExtractClaimItems(Claims);
    }

    private IReadOnlyList<string> ExtractClaimItems(string? claimsJson)
    {
        if (string.IsNullOrWhiteSpace(claimsJson))
            return [];

        var requestedClaims = _authorizationService.ParseRequestedClaims(claimsJson);
        return [.. requestedClaims.IdTokenClaimRequests
            .Concat(requestedClaims.UserInfoClaimRequests)
            .Select(kvp => BuildClaimDisplayText(kvp.Key, kvp.Value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)];
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
            ["response_mode"] = ResponseMode,
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

    private static string? ResolveResponseMode(string? responseMode)
    {
        if (string.IsNullOrWhiteSpace(responseMode)
            || string.Equals(responseMode, "query", StringComparison.Ordinal))
            return "query";

        return string.Equals(responseMode, "form_post", StringComparison.Ordinal)
            ? "form_post"
            : null;
    }

    private IActionResult BuildAuthorizationResponse(
        string redirectUri,
        string responseMode,
        IReadOnlyDictionary<string, string?> parameters)
    {
        var presentParameters = parameters
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.Ordinal);

        if (!string.Equals(responseMode, "form_post", StringComparison.Ordinal))
        {
            var redirectUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
                redirectUri,
                presentParameters);
            return new RedirectResult(redirectUrl);
        }

        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["Pragma"] = "no-cache";
        var action = System.Net.WebUtility.HtmlEncode(redirectUri);
        var hiddenInputs = string.Join(
            Environment.NewLine,
            presentParameters.Select(pair =>
                $"<input type=\"hidden\" name=\"{System.Net.WebUtility.HtmlEncode(pair.Key)}\" value=\"{System.Net.WebUtility.HtmlEncode(pair.Value)}\" />"));
        var html = $"<!doctype html><html><head><meta charset=\"utf-8\"><title>Submitting authorization response</title></head><body><form method=\"post\" action=\"{action}\">{hiddenInputs}<noscript><button type=\"submit\">Continue</button></noscript></form><script>document.forms[0].submit();</script></body></html>";
        return new ContentResult
        {
            Content = html,
            ContentType = "text/html; charset=utf-8",
            StatusCode = StatusCodes.Status200OK,
        };
    }
}
