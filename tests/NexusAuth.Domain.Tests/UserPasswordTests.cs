using NexusAuth.Domain.AggregateRoots.Users;
using NexusAuth.Domain.Entities;
using Xunit;

namespace NexusAuth.Domain.Tests;

public sealed class UserPasswordTests
{
    [Fact]
    public void ChangePassword_replaces_the_hash_and_verifies_only_the_new_password()
    {
        var user = User.Create("alice", "OldPassword!1", "Alice");
        var originalHash = user.PasswordHash;

        user.ChangePassword("NewPassword!2");

        Assert.NotEqual(originalHash, user.PasswordHash);
        Assert.False(user.VerifyPassword("OldPassword!1"));
        Assert.True(user.VerifyPassword("NewPassword!2"));
    }

    [Fact]
    public void ChangePassword_rejects_an_empty_password()
    {
        var user = User.Create("alice", "OldPassword!1", "Alice");

        Assert.Throws<ArgumentException>(() => user.ChangePassword(" "));
    }

    [Fact]
    public void SsoSession_becomes_inactive_after_revocation()
    {
        var session = SsoSession.Create(Guid.NewGuid(), TimeSpan.FromHours(1));

        Assert.True(session.IsActive(DateTimeOffset.UtcNow));

        session.Revoke(DateTimeOffset.UtcNow);

        Assert.False(session.IsActive(DateTimeOffset.UtcNow));
    }
}
