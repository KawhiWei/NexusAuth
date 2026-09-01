using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace NexusAuth.Host.Authentication;

public sealed class LoginFlowOptions
{
    public const string SectionName = "LoginFlow";

    public int PendingStateLifetimeMinutes { get; set; } = 5;

    public int SessionLifetimeMinutes { get; set; } = 1440;

    public int RememberMeLifetimeDays { get; set; } = 3;

    public bool AllowRememberMe { get; set; } = true;

    public List<LoginFlowStepOptions> Steps { get; set; } = [];

    public void ApplyDefaults()
    {
        Steps ??= [];
        if (Steps.Count > 0)
        {
            foreach (var step in Steps)
            {
                step.Type = step.Type?.Trim().ToLowerInvariant() ?? string.Empty;
                step.Requirement = step.Requirement?.Trim().ToLowerInvariant() ?? string.Empty;
            }
        }
        else
        {
            Steps =
            [
                new() { Type = LoginFlowStepTypes.Password, Requirement = LoginFlowRequirements.Required },
                new() { Type = LoginFlowStepTypes.Totp, Requirement = LoginFlowRequirements.Conditional },
            ];
        }
    }

    public LoginFlowStepOptions? FindStep(string type) =>
        Steps.FirstOrDefault(step => string.Equals(step.Type, type, StringComparison.OrdinalIgnoreCase));

    public AuthenticationProperties CreateAuthenticationProperties(bool rememberMe, DateTimeOffset issuedAt)
    {
        var useRememberMe = AllowRememberMe && rememberMe;
        var lifetime = useRememberMe
            ? TimeSpan.FromDays(RememberMeLifetimeDays)
            : TimeSpan.FromMinutes(SessionLifetimeMinutes);

        return new AuthenticationProperties
        {
            IsPersistent = useRememberMe,
            IssuedUtc = issuedAt,
            ExpiresUtc = issuedAt.Add(lifetime),
            AllowRefresh = useRememberMe ? false : null,
        };
    }
}

public sealed class LoginFlowStepOptions
{
    public string Type { get; set; } = string.Empty;

    public string Requirement { get; set; } = LoginFlowRequirements.Required;
}

public static class LoginFlowStepTypes
{
    public const string Password = "password";
    public const string Totp = "totp";
}

public static class LoginFlowRequirements
{
    public const string Required = "required";
    public const string Conditional = "conditional";
}

public sealed class LoginFlowOptionsValidator : IValidateOptions<LoginFlowOptions>
{
    public ValidateOptionsResult Validate(string? name, LoginFlowOptions options)
    {
        var errors = new List<string>();
        if (options.PendingStateLifetimeMinutes is < 1 or > 15)
            errors.Add("LoginFlow:PendingStateLifetimeMinutes must be between 1 and 15.");

        if (options.SessionLifetimeMinutes is < 5 or > 1440)
            errors.Add("LoginFlow:SessionLifetimeMinutes must be between 5 and 1440.");

        if (options.RememberMeLifetimeDays is < 1 or > 30)
            errors.Add("LoginFlow:RememberMeLifetimeDays must be between 1 and 30.");

        if (options.Steps is null || options.Steps.Count == 0)
        {
            errors.Add("LoginFlow:Steps must contain at least the password step.");
            return ValidateOptionsResult.Fail(errors);
        }

        var normalizedTypes = options.Steps
            .Select(step => step.Type.Trim().ToLowerInvariant())
            .ToArray();
        if (normalizedTypes.Length > 0 && normalizedTypes[0] != LoginFlowStepTypes.Password)
            errors.Add("The first login flow step must be 'password'.");

        if (normalizedTypes.Count(type => type == LoginFlowStepTypes.Password) != 1)
            errors.Add("The login flow must contain exactly one 'password' step.");

        if (normalizedTypes.Distinct(StringComparer.Ordinal).Count() != normalizedTypes.Length)
            errors.Add("Login flow step types must be unique.");

        foreach (var step in options.Steps)
        {
            var type = step.Type.Trim().ToLowerInvariant();
            var requirement = step.Requirement.Trim().ToLowerInvariant();
            if (type is not LoginFlowStepTypes.Password and not LoginFlowStepTypes.Totp)
                errors.Add($"Unsupported login flow step '{step.Type}'.");

            if (type == LoginFlowStepTypes.Password && requirement != LoginFlowRequirements.Required)
                errors.Add("The password step must use requirement 'required'.");

            if (type == LoginFlowStepTypes.Totp
                && requirement is not LoginFlowRequirements.Required and not LoginFlowRequirements.Conditional)
            {
                errors.Add("The TOTP step requirement must be 'required' or 'conditional'.");
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
