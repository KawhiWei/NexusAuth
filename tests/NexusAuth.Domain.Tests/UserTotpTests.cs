using NexusAuth.Domain.Entities;
using Xunit;

namespace NexusAuth.Domain.Tests;

public sealed class UserTotpTests
{
    [Fact]
    public void Enrollment_only_enables_totp_after_confirmation()
    {
        var now = DateTimeOffset.UtcNow;
        var credential = UserCredential.CreatePendingTotp(
            Guid.NewGuid(), "protected-secret", now.AddMinutes(5), now: now);

        Assert.False(credential.IsEnabled);
        Assert.True(credential.ConfirmTotp("protected-secret", 42, now.AddSeconds(1)));
        Assert.True(credential.IsEnabled);
        Assert.Equal("protected-secret", credential.SecretProtected);
        Assert.Equal(42, credential.LastUsedCounter);
    }

    [Fact]
    public void Expired_enrollment_does_not_enable_totp()
    {
        var now = DateTimeOffset.UtcNow;
        var credential = UserCredential.CreatePendingTotp(
            Guid.NewGuid(), "protected-secret", now.AddSeconds(1), now: now);

        Assert.False(credential.ConfirmTotp("protected-secret", 42, now.AddSeconds(2)));
        Assert.False(credential.IsEnabled);
        Assert.NotNull(credential.PendingSecretProtected);
    }

    [Fact]
    public void Separate_authenticator_credentials_keep_replay_counters_independent()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var first = UserCredential.CreatePendingTotp(userId, "first-secret", now.AddMinutes(5), now: now);
        var second = UserCredential.CreatePendingTotp(userId, "second-secret", now.AddMinutes(5), now: now);
        first.ConfirmTotp("first-secret", 1, now);
        second.ConfirmTotp("second-secret", 1, now);

        Assert.True(first.TryUseTotpCounter(2, now.AddSeconds(1)));
        Assert.True(second.TryUseTotpCounter(2, now.AddSeconds(1)));
        Assert.False(first.TryUseTotpCounter(2, now.AddSeconds(2)));
    }
}
