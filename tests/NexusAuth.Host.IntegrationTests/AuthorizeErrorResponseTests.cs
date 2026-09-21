using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using NexusAuth.Application;
using NexusAuth.Application.Clients;
using NexusAuth.Application.Services.Authorization;
using NexusAuth.Application.Services.OIDC;
using NexusAuth.Domain.AggregateRoots.OAuthClients;
using NexusAuth.Domain.Entities;
using NexusAuth.Host.Controllers;
using Xunit;

namespace NexusAuth.Host.IntegrationTests;

public sealed class AuthorizeErrorResponseTests
{
    private const string RedirectUri = "https://client.example/callback";

    [Fact]
    public async Task Validated_redirect_uri_receives_query_error_and_preserves_state()
    {
        var controller = CreateController();

        var result = await controller.Authorize(
            responseType: null,
            clientId: "client-1",
            redirectUri: RedirectUri,
            scope: "openid",
            state: "state-123");

        var redirect = Assert.IsType<RedirectResult>(result);
        var uri = new Uri(redirect.Url!);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal(RedirectUri, uri.GetLeftPart(UriPartial.Path));
        Assert.Equal("invalid_request", query["error"]);
        Assert.Equal("response_type is required.", query["error_description"]);
        Assert.Equal("state-123", query["state"]);
    }

    [Fact]
    public async Task Validated_redirect_uri_receives_form_post_error_and_preserves_state()
    {
        var controller = CreateController();

        var result = await controller.Authorize(
            responseType: "token",
            clientId: "client-1",
            redirectUri: RedirectUri,
            scope: "openid",
            state: "state-<&>\"",
            responseMode: "form_post");

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal("text/html; charset=utf-8", content.ContentType);
        Assert.Contains($"method=\"post\" action=\"{RedirectUri}\"", content.Content);
        Assert.Contains("name=\"error\" value=\"unsupported_response_type\"", content.Content);
        Assert.Contains("name=\"error_description\" value=\"Only &#39;code&#39; response type is supported.\"", content.Content);
        Assert.Contains("name=\"state\" value=\"state-&lt;&amp;&gt;&quot;\"", content.Content);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
        Assert.Equal("no-cache", controller.Response.Headers.Pragma);
    }

    private static AuthorizeController CreateController()
    {
        var controller = new AuthorizeController(
            new UnusedAuthorizationService(),
            new RedirectValidatingClientService());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        return controller;
    }

    private sealed class UnusedAuthorizationService : IAuthorizationService
    {
        public Task<string> GenerateCodeAsync(Guid userId, string clientId, string redirectUri, string scope, string? codeChallenge = null, string? codeChallengeMethod = null, string? nonce = null, string? claimsJson = null, DateTimeOffset? authenticatedAt = null, string? acr = null, string? amr = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthorizationCodeResult> ValidateAndConsumeCodeAsync(string code, string clientId, string redirectUri, string? codeVerifier = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ClientCredentialsResult> ValidateClientCredentialsAsync(ClientAuthenticationInput authentication, string scope, CancellationToken ct = default) => throw new NotSupportedException();
        public OidcRequestedClaims ParseRequestedClaims(string? claimsJson) => throw new NotSupportedException();
    }

    private sealed class RedirectValidatingClientService : IClientService
    {
        public Task<ClientValidationResult> ValidateClientRedirectUriAsync(string clientId, string redirectUri, CancellationToken ct = default) =>
            Task.FromResult(ClientValidationResult.Success());

        public Task<OAuthClient> RegisterClientAsync(string clientId, string clientName, string? description = null, IEnumerable<string>? redirectUris = null, IEnumerable<string>? postLogoutRedirectUris = null, IEnumerable<string>? allowedScopes = null, IEnumerable<string>? allowedGrantTypes = null, bool requirePkce = true, string tokenEndpointAuthMethod = OAuthClient.TokenEndpointAuthMethodClientSecretBasic, IEnumerable<OAuthClientSecret>? clientSecrets = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OAuthClient?> ValidateClientAsync(string clientId, string rawClientSecret, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ClientValidationResult> ValidateClientForAuthorizationAsync(string clientId, string redirectUri, string grantType, string? codeChallenge = null, string? codeChallengeMethod = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ClientAuthenticationResult> AuthenticateClientAsync(string clientId, string? rawClientSecret, bool requireSecret, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ClientAuthenticationResult> AuthenticateClientAsync(ClientAuthenticationInput input, bool requireClientAuthentication, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ClientAuthenticationResult> AuthenticateClientForPostLogoutAsync(string clientId, string? postLogoutRedirectUri, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ScopeValidationResult> ValidateScopesAsync(string clientId, string scope, bool allowIdentityScopes, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<ClientDto>> GetAllAsync(string? keyword = null, bool? isActive = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PagedResult<ClientDto>> GetPagedAsync(string? keyword = null, bool? isActive = null, int page = 1, int pageSize = 10, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ClientDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ClientMutationResultDto> CreateAsync(CreateClientRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ClientDto> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ClientMutationResultDto> GenerateCredentialAsync(Guid id, GenerateClientCredentialRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ClientMutationResultDto> ResetCredentialAsync(Guid id, GenerateClientCredentialRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
