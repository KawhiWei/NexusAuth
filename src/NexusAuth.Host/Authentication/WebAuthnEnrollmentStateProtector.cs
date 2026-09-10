using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace NexusAuth.Host.Authentication;

/// <summary>注册成功后传递到 Passkey 建档页的短期受保护状态。</summary>
public sealed record WebAuthnEnrollmentState(Guid UserId, string? ReturnUrl);

/// <summary>使用 ASP.NET Core Data Protection 签名并限时保护建档状态。</summary>
public sealed class WebAuthnEnrollmentStateProtector
{
    private const string Purpose = "WebAuthnNexusAuth.PasskeyEnrollment.v1";
    private readonly ITimeLimitedDataProtector _protector;

    public WebAuthnEnrollmentStateProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();
    }

    public string Protect(WebAuthnEnrollmentState state, TimeSpan lifetime) =>
        _protector.Protect(JsonSerializer.Serialize(state), lifetime);

    /// <summary>验证令牌签名与有效期，并还原可信的用户和返回地址。</summary>
    public bool TryUnprotect(string? token, out WebAuthnEnrollmentState? state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            state = JsonSerializer.Deserialize<WebAuthnEnrollmentState>(_protector.Unprotect(token));
            return state is not null && state.UserId != Guid.Empty;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
