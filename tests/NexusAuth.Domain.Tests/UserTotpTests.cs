using NexusAuth.Domain.AggregateRoots.Users;
using Xunit;

namespace NexusAuth.Domain.Tests;

public sealed class UserTotpTests
{
    [Fact]
    public void Enrollment_only_enables_totp_after_confirmation()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Create("alice", "Password!1", "Alice");

        user.BeginTotpEnrollment("protected-secret", now.AddMinutes(5), now);

        Assert.False(user.TotpEnabled);
        Assert.True(user.ConfirmTotpEnrollment(42, now.AddSeconds(1)));
        Assert.True(user.TotpEnabled);
        Assert.Equal("protected-secret", user.TotpSecretProtected);
        Assert.Equal(42, user.TotpLastUsedCounter);
    }

    [Fact]
    public void Expired_enrollment_does_not_enable_totp()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Create("alice", "Password!1", "Alice");
        user.BeginTotpEnrollment("protected-secret", now.AddSeconds(1), now);

        Assert.False(user.ConfirmTotpEnrollment(42, now.AddSeconds(2)));
        Assert.False(user.TotpEnabled);
        Assert.Null(user.TotpPendingSecretProtected);
    }
}
