using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NexusAuth.Application.Services.Sessions;
using NexusAuth.Application.Services.Tokens;
using NexusAuth.Host;
using NexusAuth.Host.Controllers;
using NexusAuth.Host.Pages.Account;
using Xunit;

namespace NexusAuth.Host.IntegrationTests;

public sealed class EndSessionTests
{
    [Fact]
    public async Task Get_only_renders_post_confirmation_and_does_not_revoke_session()
    {
        var sessions = new RecordingSsoSessionService();
        var tokens = new RecordingTokenService();
        var controller = CreateController(sessions, tokens, out _);

        var result = await controller.EndSession(state: "state-123");

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode ?? StatusCodes.Status200OK);
        Assert.Equal("text/html; charset=utf-8", content.ContentType);
        Assert.Contains("<form method=\"post\" action=\"/connect/endsession\">", content.Content);
        Assert.Contains("name=\"__RequestVerificationToken\" value=\"test-request-token\"", content.Content);
        Assert.Contains("name=\"state\" value=\"state-123\"", content.Content);
        Assert.Equal(0, sessions.RevokeCount);
        Assert.Equal(0, sessions.RevokeAllCount);
        Assert.Empty(tokens.RevokedUsers);
    }

    [Fact]
    public async Task Post_revokes_all_user_sessions_and_tokens_and_signs_out_cookie()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var sessions = new RecordingSsoSessionService();
        var tokens = new RecordingTokenService();
        var controller = CreateController(sessions, tokens, out var authentication);
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sid", sessionId.ToString()),
        ], AppWebModule.AuthenticationScheme));

        var result = await controller.ConfirmEndSession(state: "state-123");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/", redirect.Url);
        Assert.Equal(0, sessions.RevokeCount);
        Assert.Equal(1, sessions.RevokeAllCount);
        Assert.Equal(userId, sessions.LastRevokedUser);
        Assert.Equal([userId], tokens.RevokedUsers);
        Assert.Equal(AppWebModule.AuthenticationScheme, authentication.SignedOutScheme);
    }

    [Fact]
    public async Task Post_without_valid_sid_does_not_revoke_other_sessions_or_tokens()
    {
        var sessions = new RecordingSsoSessionService();
        var tokens = new RecordingTokenService();
        var controller = CreateController(sessions, tokens, out var authentication);
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], AppWebModule.AuthenticationScheme));

        await controller.ConfirmEndSession();

        Assert.Equal(0, sessions.RevokeAllCount);
        Assert.Empty(tokens.RevokedUsers);
        Assert.Equal(AppWebModule.AuthenticationScheme, authentication.SignedOutScheme);
    }

    [Fact]
    public async Task Account_logout_revokes_all_user_sessions_and_tokens()
    {
        var userId = Guid.NewGuid();
        var sessions = new RecordingSsoSessionService();
        var tokens = new RecordingTokenService();
        var authentication = new RecordingAuthenticationService();
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider();
        var model = new IndexModel(sessions, tokens)
        {
            PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services },
            },
        };
        model.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim("sid", Guid.NewGuid().ToString())],
        AppWebModule.AuthenticationScheme));

        var result = await model.OnPostLogoutAsync();

        Assert.Equal("/Account/Login", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Equal(1, sessions.RevokeAllCount);
        Assert.Equal(userId, sessions.LastRevokedUser);
        Assert.Equal([userId], tokens.RevokedUsers);
        Assert.Equal(AppWebModule.AuthenticationScheme, authentication.SignedOutScheme);
    }

    [Fact]
    public async Task Account_logout_without_valid_sid_only_signs_out_cookie()
    {
        var sessions = new RecordingSsoSessionService();
        var tokens = new RecordingTokenService();
        var authentication = new RecordingAuthenticationService();
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider();
        var model = new IndexModel(sessions, tokens)
        {
            PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services },
            },
        };
        model.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], AppWebModule.AuthenticationScheme));

        await model.OnPostLogoutAsync();

        Assert.Equal(0, sessions.RevokeAllCount);
        Assert.Empty(tokens.RevokedUsers);
        Assert.Equal(AppWebModule.AuthenticationScheme, authentication.SignedOutScheme);
    }

    private static OpenIdController CreateController(
        RecordingSsoSessionService sessions,
        RecordingTokenService tokens,
        out RecordingAuthenticationService authentication)
    {
        authentication = new RecordingAuthenticationService();
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authentication)
            .BuildServiceProvider();
        var controller = new OpenIdController(
            signingCredentialsProvider: null!,
            idTokenHintValidator: null!,
            tokenService: tokens,
            userService: null!,
            clientService: null!,
            deviceAuthorizationService: null!,
            sessionService: sessions,
            antiforgery: new TestAntiforgery(),
            jwtOptions: Options.Create(new JwtOptions { Issuer = "https://issuer.example" }));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services },
        };
        return controller;
    }

    private sealed class RecordingSsoSessionService : ISsoSessionService
    {
        public int RevokeCount { get; private set; }
        public int RevokeAllCount { get; private set; }
        public Guid? LastRevokedUser { get; private set; }
        public (Guid SessionId, Guid UserId)? LastRevocation { get; private set; }

        public Task RevokeAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
        {
            RevokeCount++;
            LastRevocation = (sessionId, userId);
            return Task.CompletedTask;
        }

        public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
        {
            RevokeAllCount++;
            LastRevokedUser = userId;
            return Task.CompletedTask;
        }

        public Task<Guid> CreateAsync(Guid userId, TimeSpan lifetime, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsActiveAsync(Guid sessionId, Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class RecordingTokenService : ITokenService
    {
        public List<Guid> RevokedUsers { get; } = [];

        public Task RevokeAllUserTokensAsync(Guid userId, CancellationToken ct = default)
        {
            RevokedUsers.Add(userId);
            return Task.CompletedTask;
        }

        public Task<string> IssueAccessTokenAsync(string clientId, string scope, string? audience = null, Guid? userId = null, string? claimsJson = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TokenIssueResult> IssueAccessTokenWithMetadataAsync(string clientId, string scope, string? audience = null, Guid? userId = null, string? claimsJson = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> IssueIdTokenAsync(string clientId, Guid userId, string? nonce, string accessToken, string? claimsJson = null, DateTimeOffset? authenticatedAt = null, string? acr = null, string? amr = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> IssueRefreshTokenAsync(string clientId, Guid userId, string scope, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RefreshResult> RefreshAsync(string refreshTokenString, string? clientId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RevokeRefreshTokenAsync(string refreshTokenString, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RevokeRefreshTokenAsync(string refreshTokenString, string? clientId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsRefreshTokenOwnedByClientAsync(string refreshTokenString, string clientId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TokenIntrospectionResult> IntrospectAsync(string token, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TokenIntrospectionResult> IntrospectAsync(string token, string? clientId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RevokeAccessTokenAsync(string accessToken, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RevokeAccessTokenAsync(string accessToken, string? clientId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class TestAntiforgery : IAntiforgery
    {
        private static readonly AntiforgeryTokenSet Tokens = new(
            "test-request-token",
            "test-cookie-token",
            "__RequestVerificationToken",
            "RequestVerificationToken");

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => Tokens;
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => Tokens;
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public string? SignedOutScheme { get; private set; }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignedOutScheme = scheme;
            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => throw new NotSupportedException();
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => throw new NotSupportedException();
    }
}
