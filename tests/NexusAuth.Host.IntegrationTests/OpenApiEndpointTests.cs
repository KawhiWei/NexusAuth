using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NexusAuth.Application;
using NexusAuth.Application.Clients;
using NexusAuth.Application.Services.ApiResources;
using NexusAuth.Application.Services.OpenApi;
using NexusAuth.Domain.AggregateRoots.ApiResources;
using NexusAuth.Domain.AggregateRoots.OAuthClients;
using NexusAuth.Domain.Entities;
using Xunit;

namespace NexusAuth.Host.IntegrationTests;

public sealed class OpenApiEndpointTests : IClassFixture<OpenApiFactory>
{
    private readonly OpenApiFactory factory;

    public OpenApiEndpointTests(OpenApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task Application_credential_reads_applications_without_management_secrets()
    {
        using var client = factory.CreateClient();
        UseBearer(client, OpenApiFactory.ApplicationToken);

        var response = await client.GetAsync("/openapi/v1/applications?keyword=permission");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var application = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("permission-center-api", application.GetProperty("clientId").GetString());
        Assert.Equal("Permission Center", application.GetProperty("clientName").GetString());
        Assert.False(application.TryGetProperty("credentials", out _));
        Assert.False(application.TryGetProperty("redirectUris", out _));
        Assert.False(application.TryGetProperty("allowedScopes", out _));
    }

    [Fact]
    public async Task Service_resource_credential_reads_service_resources()
    {
        using var client = factory.CreateClient();
        UseBearer(client, OpenApiFactory.ServiceResourceToken);

        var response = await client.GetAsync("/openapi/v1/service-resources");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var resource = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("permission-center-api", resource.GetProperty("name").GetString());
        Assert.Equal("permission-center-api", resource.GetProperty("audience").GetString());
    }

    [Theory]
    [InlineData("/openapi/v1/applications")]
    [InlineData("/openapi/v1/service-resources")]
    public async Task Missing_or_unknown_credential_is_rejected(string path)
    {
        using (var anonymous = factory.CreateClient())
        {
            var response = await anonymous.GetAsync(path);
            await AssertInvalidTokenAsync(response);
        }

        using var client = factory.CreateClient();
        UseBearer(client, "unknown-token");
        await AssertInvalidTokenAsync(await client.GetAsync(path));
    }

    [Fact]
    public async Task Credential_cannot_cross_the_target_type_boundary()
    {
        using (var applicationClient = factory.CreateClient())
        {
            UseBearer(applicationClient, OpenApiFactory.ApplicationToken);
            await AssertInvalidTokenAsync(await applicationClient.GetAsync("/openapi/v1/service-resources"));
        }

        using var resourceClient = factory.CreateClient();
        UseBearer(resourceClient, OpenApiFactory.ServiceResourceToken);
        await AssertInvalidTokenAsync(await resourceClient.GetAsync("/openapi/v1/applications"));
    }

    [Theory]
    [InlineData(OpenApiFactory.ExpiredApplicationToken)]
    [InlineData(OpenApiFactory.RevokedApplicationToken)]
    public async Task Expired_or_revoked_credential_is_rejected(string token)
    {
        using var client = factory.CreateClient();
        UseBearer(client, token);
        await AssertInvalidTokenAsync(await client.GetAsync("/openapi/v1/applications"));
    }

    private static void UseBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task AssertInvalidTokenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_token", document.RootElement.GetProperty("error").GetString());
    }
}

public sealed class OpenApiFactory : WebApplicationFactory<AppWebModule>
{
    public const string ApplicationToken = "application-directory-token";
    public const string ServiceResourceToken = "service-resource-directory-token";
    public const string ExpiredApplicationToken = "expired-application-directory-token";
    public const string RevokedApplicationToken = "revoked-application-directory-token";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development")
            .UseSetting("BootstrapAdmin:Username", string.Empty)
            .UseSetting("BootstrapAdmin:Password", string.Empty)
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<IOpenApiCredentialService>();
                services.RemoveAll<IClientService>();
                services.RemoveAll<IApiResourceService>();
                services.AddSingleton<IOpenApiCredentialService, TestOpenApiCredentialService>();
                services.AddSingleton<IClientService, TestOpenApiClientService>();
                services.AddSingleton<IApiResourceService, TestOpenApiResourceService>();
            });
    }
}

public sealed class TestOpenApiCredentialService : IOpenApiCredentialService
{
    public Task<bool> ValidateAsync(string rawToken, string targetType, CancellationToken ct = default) =>
        Task.FromResult((rawToken, targetType) switch
        {
            (OpenApiFactory.ApplicationToken, OpenApiCredential.TargetTypeApplication) => true,
            (OpenApiFactory.ServiceResourceToken, OpenApiCredential.TargetTypeServiceResource) => true,
            _ => false,
        });

    public Task<OpenApiCredentialCreated> CreateAsync(string name, string targetType, DateTimeOffset? expiresAt, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<OpenApiCredentialSummary>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<OpenApiCredentialSummary?> UpdateAsync(Guid id, string name, DateTimeOffset? expiresAt, bool isActive, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> RevokeAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
}

public sealed class TestOpenApiClientService : IClientService
{
    public Task<List<ClientDto>> GetAllAsync(string? keyword = null, bool? isActive = null, CancellationToken ct = default)
    {
        var clients = new List<ClientDto>
        {
            new(Guid.NewGuid(), "permission-center-api", [], OAuthClient.TokenEndpointAuthMethodClientSecretBasic, null, null, "Permission Center", "RBAC service", ["https://internal.example/callback"], [], ["sensitive:scope"], ["client_credentials"], false, true, DateTimeOffset.UtcNow),
        };
        return Task.FromResult(string.IsNullOrWhiteSpace(keyword) ? clients : clients.Where(item => item.ClientId.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList());
    }

    public Task<OAuthClient> RegisterClientAsync(string clientId, string clientName, string? description = null, IEnumerable<string>? redirectUris = null, IEnumerable<string>? postLogoutRedirectUris = null, IEnumerable<string>? allowedScopes = null, IEnumerable<string>? allowedGrantTypes = null, bool requirePkce = true, string tokenEndpointAuthMethod = OAuthClient.TokenEndpointAuthMethodClientSecretBasic, IEnumerable<OAuthClientSecret>? clientSecrets = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<OAuthClient?> ValidateClientAsync(string clientId, string rawClientSecret, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ClientValidationResult> ValidateClientForAuthorizationAsync(string clientId, string redirectUri, string grantType, string? codeChallenge = null, string? codeChallengeMethod = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ClientValidationResult> ValidateClientRedirectUriAsync(string clientId, string redirectUri, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ClientAuthenticationResult> AuthenticateClientAsync(string clientId, string? rawClientSecret, bool requireSecret, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ClientAuthenticationResult> AuthenticateClientAsync(ClientAuthenticationInput input, bool requireClientAuthentication, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ClientAuthenticationResult> AuthenticateClientForPostLogoutAsync(string clientId, string? postLogoutRedirectUri, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ScopeValidationResult> ValidateScopesAsync(string clientId, string scope, bool allowIdentityScopes, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<PagedResult<ClientDto>> GetPagedAsync(string? keyword = null, bool? isActive = null, int page = 1, int pageSize = 10, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ClientDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ClientMutationResultDto> CreateAsync(CreateClientRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ClientDto> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ClientMutationResultDto> GenerateCredentialAsync(Guid id, GenerateClientCredentialRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ClientMutationResultDto> ResetCredentialAsync(Guid id, GenerateClientCredentialRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
}

public sealed class TestOpenApiResourceService : IApiResourceService
{
    public Task<List<ApiResourceDto>> GetAllAsync(string? keyword = null, bool? isActive = null, CancellationToken ct = default) =>
        Task.FromResult(new List<ApiResourceDto> { new(Guid.NewGuid(), "permission-center-api", "Permission Center API", "permission-center-api", "RBAC API", true, DateTimeOffset.UtcNow) });

    public Task<PagedResult<ApiResourceDto>> GetPagedAsync(string? keyword = null, bool? isActive = null, int page = 1, int pageSize = 10, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ApiResourceDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ApiResource> RegisterAsync(string name, string displayName, string audience, string? description = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ApiResourceDto> CreateAsync(CreateApiResourceRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ApiResourceDto> UpdateAsync(Guid id, UpdateApiResourceRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ApiResourceDto> UpdateStatusAsync(Guid id, bool isActive, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task AssignToClientAsync(Guid clientId, Guid apiResourceId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task RevokeFromClientAsync(Guid clientId, Guid apiResourceId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<ApiResource>> GetClientResourcesAsync(Guid clientId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<ApiResource>> GetAllActiveResourcesAsync(CancellationToken ct = default) => throw new NotSupportedException();
}
