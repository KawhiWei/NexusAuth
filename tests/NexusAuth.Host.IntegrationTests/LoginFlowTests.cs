using Microsoft.AspNetCore.DataProtection;
using NexusAuth.Application.Users;
using NexusAuth.Host.Authentication;
using Xunit;

namespace NexusAuth.Host.IntegrationTests;

public sealed class LoginFlowTests
{
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Fact]
    public void Totp_matches_the_rfc_6238_sha1_vector_truncated_to_six_digits()
    {
        Assert.Equal("287082", TotpAlgorithm.ComputeCode(RfcSecret, 1));
    }

    [Fact]
    public void Totp_accepts_one_adjacent_time_step()
    {
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(59);
        var previousCode = TotpAlgorithm.ComputeCode(RfcSecret, 0);

        Assert.True(TotpAlgorithm.TryFindCounter(RfcSecret, previousCode, timestamp, out var counter));
        Assert.Equal(0, counter);
    }

    [Fact]
    public void Login_flow_rejects_an_unknown_step()
    {
        var options = new LoginFlowOptions
        {
            Steps =
            [
                new() { Type = "password", Requirement = "required" },
                new() { Type = "magic", Requirement = "conditional" },
            ],
        };

        var result = new LoginFlowOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Login_flow_normalizes_configured_step_values()
    {
        var options = new LoginFlowOptions
        {
            Steps =
            [
                new() { Type = " PASSWORD ", Requirement = " REQUIRED " },
                new() { Type = " TOTP ", Requirement = " CONDITIONAL " },
            ],
        };

        options.ApplyDefaults();

        Assert.Equal(LoginFlowRequirements.Conditional, options.FindStep(LoginFlowStepTypes.Totp)?.Requirement);
        Assert.False(new LoginFlowOptionsValidator().Validate(null, options).Failed);
    }

    [Fact]
    public void Pending_login_state_rejects_tampering()
    {
        var protector = new LoginFlowStateProtector(new EphemeralDataProtectionProvider());
        var token = protector.Protect(
            new PendingLoginFlowState(Guid.NewGuid(), "alice", "/", false, 123),
            TimeSpan.FromMinutes(1));
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        Assert.False(protector.TryUnprotect(tampered, out _));
    }
}
