using NexusAuth.Domain.Entities;
using Xunit;

namespace NexusAuth.Domain.Tests;

public sealed class WebAuthnChallengeTests
{
    [Fact]
    public void Authentication_challenge_can_be_consumed_once_and_assigned_to_a_user()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var challenge = WebAuthnChallenge.Create(
            "flow-token",
            WebAuthnChallenge.AuthenticationPurpose,
            userId: null,
            optionsJson: "{}",
            returnUrl: null,
            rememberMe: false,
            expiresAt: now.AddMinutes(5),
            now);

        Assert.True(challenge.TryConsume(now.AddSeconds(1)));
        Assert.False(challenge.TryConsume(now.AddSeconds(2)));

        challenge.AssignAuthenticatedUser(userId);

        Assert.Equal(userId, challenge.UserId);
        Assert.Equal(now.AddSeconds(1), challenge.ConsumedAt);
    }

    [Fact]
    public void Expired_challenge_cannot_be_consumed()
    {
        var now = DateTimeOffset.UtcNow;
        var challenge = WebAuthnChallenge.Create(
            "flow-token",
            WebAuthnChallenge.AuthenticationPurpose,
            userId: null,
            optionsJson: "{}",
            returnUrl: null,
            rememberMe: false,
            expiresAt: now.AddMinutes(5),
            now);

        Assert.False(challenge.TryConsume(now.AddMinutes(5)));
        Assert.Null(challenge.ConsumedAt);
    }

    [Fact]
    public void Registration_challenge_cannot_assign_another_user()
    {
        var now = DateTimeOffset.UtcNow;
        var challenge = WebAuthnChallenge.Create(
            "flow-token",
            WebAuthnChallenge.RegistrationPurpose,
            Guid.NewGuid(),
            optionsJson: "{}",
            returnUrl: null,
            rememberMe: false,
            expiresAt: now.AddMinutes(5),
            now);

        Assert.True(challenge.TryConsume(now.AddSeconds(1)));
        Assert.Throws<InvalidOperationException>(() => challenge.AssignAuthenticatedUser(Guid.NewGuid()));
    }
}
