using Fido2NetLib.Objects;
using Microsoft.Extensions.Options;

namespace NexusAuth.Host.Authentication;

/// <summary>
/// WebAuthn Relying Party 配置。RP ID、Origin 与浏览器实际访问域名必须匹配。
/// </summary>
public sealed class WebAuthnOptions
{
    public const string SectionName = "WebAuthn";

    /// <summary>控制 Web 端是否提供 Passkey 注册与登录能力。</summary>
    public bool Enabled { get; set; }
    public string RelyingPartyId { get; set; } = "localhost";
    public string RelyingPartyName { get; set; } = "WebAuthn NexusAuth";
    public List<string> Origins { get; set; } = ["http://localhost:5200"];
    public int ChallengeLifetimeSeconds { get; set; } = 300;
    public UserVerificationRequirement UserVerification { get; set; } = UserVerificationRequirement.Required;
}

/// <summary>启动时校验 WebAuthn 配置，避免错误域名导致浏览器静默拒绝。</summary>
public sealed class WebAuthnOptionsValidator : IValidateOptions<WebAuthnOptions>
{
    public ValidateOptionsResult Validate(string? name, WebAuthnOptions options)
    {
        // 功能关闭时不要求部署方提供 RP ID 和 Origin。
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.RelyingPartyId)
            || options.RelyingPartyId.Contains("://", StringComparison.Ordinal)
            || options.RelyingPartyId.Contains('/'))
        {
            errors.Add("WebAuthn:RelyingPartyId must be a domain name without a scheme or path.");
        }

        if (string.IsNullOrWhiteSpace(options.RelyingPartyName))
            errors.Add("WebAuthn:RelyingPartyName is required.");

        if (options.ChallengeLifetimeSeconds is < 60 or > 900)
            errors.Add("WebAuthn:ChallengeLifetimeSeconds must be between 60 and 900.");

        if (options.Origins is null || options.Origins.Count == 0
            || options.Origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || uri.Scheme is not "https" and not "http" || !string.IsNullOrEmpty(uri.AbsolutePath.Trim('/'))))
        {
            errors.Add("WebAuthn:Origins must contain absolute origin URLs without a path.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
