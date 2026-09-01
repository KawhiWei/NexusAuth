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
    public void Remember_me_lifetime_defaults_to_three_days()
    {
        Assert.Equal(3, new LoginFlowOptions().RememberMeLifetimeDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Login_flow_rejects_an_out_of_range_remember_me_lifetime(int lifetimeDays)
    {
        var options = new LoginFlowOptions
        {
            RememberMeLifetimeDays = lifetimeDays,
            Steps =
            [
                new() { Type = "password", Requirement = "required" },
            ],
        };

        var result = new LoginFlowOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Remembered_authentication_uses_an_absolute_configured_lifetime()
    {
        var issuedAt = new DateTimeOffset(2026, 9, 1, 8, 30, 0, TimeSpan.Zero);
        var options = new LoginFlowOptions
        {
            RememberMeLifetimeDays = 3,
            AllowRememberMe = true,
        };

        var properties = options.CreateAuthenticationProperties(true, issuedAt);

        Assert.True(properties.IsPersistent);
        Assert.Equal(issuedAt, properties.IssuedUtc);
        Assert.Equal(issuedAt.AddDays(3), properties.ExpiresUtc);
        Assert.Equal(false, properties.AllowRefresh);
    }

    [Fact]
    public void Unchecked_remember_me_uses_session_lifetime_and_allows_sliding_refresh()
    {
        var issuedAt = new DateTimeOffset(2026, 9, 1, 8, 30, 0, TimeSpan.Zero);
        var options = new LoginFlowOptions
        {
            SessionLifetimeMinutes = 45,
            AllowRememberMe = true,
        };

        var properties = options.CreateAuthenticationProperties(false, issuedAt);

        Assert.False(properties.IsPersistent);
        Assert.Equal(issuedAt, properties.IssuedUtc);
        Assert.Equal(issuedAt.AddMinutes(45), properties.ExpiresUtc);
        Assert.Null(properties.AllowRefresh);
    }

    [Fact]
    public void Remember_me_is_ignored_when_disabled_by_configuration()
    {
        var issuedAt = new DateTimeOffset(2026, 9, 1, 8, 30, 0, TimeSpan.Zero);
        var options = new LoginFlowOptions
        {
            SessionLifetimeMinutes = 45,
            AllowRememberMe = false,
        };

        var properties = options.CreateAuthenticationProperties(true, issuedAt);

        Assert.False(properties.IsPersistent);
        Assert.Equal(issuedAt.AddMinutes(45), properties.ExpiresUtc);
        Assert.Null(properties.AllowRefresh);
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

    [Fact]
    public void Slider_captcha_accepts_the_target_once_and_rejects_replay()
    {
        var protector = new SliderCaptchaChallengeProtector(new EphemeralDataProtectionProvider());
        var token = protector.Protect(new SliderCaptchaChallenge(120, "nonce"), TimeSpan.FromMinutes(1));

        Assert.True(protector.TryValidate(token, 123));
        Assert.False(protector.TryValidate(token, 123));
    }

    [Fact]
    public void Slider_captcha_rejects_a_tampered_token_or_an_offset_outside_tolerance()
    {
        var protector = new SliderCaptchaChallengeProtector(new EphemeralDataProtectionProvider());
        var token = protector.Protect(new SliderCaptchaChallenge(120, "nonce"), TimeSpan.FromMinutes(1));
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        Assert.False(protector.TryValidate(tampered, 120));
        Assert.False(protector.TryValidate(token, 126));
    }
}
