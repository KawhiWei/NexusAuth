using NexusAuth.Domain.Entities;
using Xunit;

namespace NexusAuth.Domain.Tests;

public sealed class OpenApiCredentialTests
{
    [Fact]
    public void ApplicationCredential_only_authenticates_application_directory_access()
    {
        var credential = OpenApiCredential.CreateWithToken(
            "permission-center-app-reader",
            "test-open-api-token",
            OpenApiCredential.TargetTypeApplication);

        Assert.Contains(OpenApiCredential.ScopeApplicationRead, credential.Scopes);
        Assert.True(credential.CanAuthenticate(
            OpenApiCredential.TargetTypeApplication,
            OpenApiCredential.ScopeApplicationRead));
        Assert.False(credential.CanAuthenticate(
            OpenApiCredential.TargetTypeServiceResource,
            OpenApiCredential.ScopeServiceResourceRead));
    }

    [Fact]
    public void RevokedCredential_cannot_authenticate()
    {
        var credential = OpenApiCredential.CreateWithToken(
            "permission-center-resource-reader",
            "test-open-api-token",
            OpenApiCredential.TargetTypeServiceResource);

        credential.Revoke();

        Assert.False(credential.CanAuthenticate(
            OpenApiCredential.TargetTypeServiceResource,
            OpenApiCredential.ScopeServiceResourceRead));
        Assert.NotNull(credential.RevokedAt);
    }

    [Fact]
    public void ExpiredCredential_cannot_authenticate()
    {
        var credential = OpenApiCredential.CreateWithToken(
            "expired-reader",
            "test-open-api-token",
            OpenApiCredential.TargetTypeApplication,
            DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.False(credential.CanAuthenticate(
            OpenApiCredential.TargetTypeApplication,
            OpenApiCredential.ScopeApplicationRead));
    }
}
