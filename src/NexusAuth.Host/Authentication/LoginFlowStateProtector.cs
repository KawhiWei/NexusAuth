using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace NexusAuth.Host.Authentication;

public sealed record PendingLoginFlowState(
    Guid UserId,
    string Username,
    string? ReturnUrl,
    bool RememberMe,
    long AuthenticatedAtUnixSeconds);

public sealed class LoginFlowStateProtector
{
    private const string Purpose = "NexusAuth.LoginFlow.PendingState.v1";
    private readonly ITimeLimitedDataProtector _protector;

    public LoginFlowStateProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose).ToTimeLimitedDataProtector();
    }

    public string Protect(PendingLoginFlowState state, TimeSpan lifetime)
    {
        return _protector.Protect(JsonSerializer.Serialize(state), lifetime);
    }

    public bool TryUnprotect(string? protectedState, out PendingLoginFlowState? state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(protectedState))
            return false;

        try
        {
            state = JsonSerializer.Deserialize<PendingLoginFlowState>(_protector.Unprotect(protectedState));
            return state is not null;
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
