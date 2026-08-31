using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Luck.DDD.Domain.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NexusAuth.Application.Services.Scim;
using NexusAuth.Application.Services.Sessions;
using NexusAuth.Domain.AggregateRoots.Users;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;
using Xunit;

namespace NexusAuth.Host.IntegrationTests;

public sealed class ScimProvisioningTests : IClassFixture<ScimProvisioningFactory>
{
    private const string ReadWriteToken = "integration-read-write-token";
    private const string ReadOnlyToken = "integration-read-only-token";
    private readonly ScimProvisioningFactory factory;

    public ScimProvisioningTests(ScimProvisioningFactory factory)
    {
        this.factory = factory;
        factory.Users.Reset();
    }

    [Fact]
    public async Task Service_provider_config_requires_a_valid_read_credential()
    {
        using var client = factory.CreateClient();

        var unauthorized = await client.GetAsync("/scim/v2/ServiceProviderConfig");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        await AssertScimResponseAsync(unauthorized, "urn:ietf:params:scim:api:messages:2.0:Error");

        UseBearer(client, ReadWriteToken);
        var response = await client.GetAsync("/scim/v2/ServiceProviderConfig");

        response.EnsureSuccessStatusCode();
        using var document = await AssertScimResponseAsync(response, "urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig");
        Assert.True(document.RootElement.GetProperty("patch").GetProperty("supported").GetBoolean());
        Assert.True(document.RootElement.GetProperty("filter").GetProperty("supported").GetBoolean());
    }

    [Fact]
    public async Task Read_only_credential_is_rejected_before_the_request_body_is_bound()
    {
        using var client = factory.CreateClient();
        UseBearer(client, ReadOnlyToken);
        using var request = ScimRequest(HttpMethod.Post, "/scim/v2/Users", "{");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = await AssertScimResponseAsync(response, "urn:ietf:params:scim:api:messages:2.0:Error");
        Assert.Equal("forbidden", document.RootElement.GetProperty("scimType").GetString());
        Assert.Empty(factory.Users.Users);
    }

    [Fact]
    public async Task User_can_be_created_filtered_replaced_patched_and_deleted()
    {
        using var client = factory.CreateClient();
        UseBearer(client, ReadWriteToken);

        var createResponse = await PostUserAsync(client, "alice", "idp-alice", "alice@example.com");
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var created = await AssertScimResponseAsync(createResponse, "urn:ietf:params:scim:schemas:core:2.0:User");
        var userId = created.RootElement.GetProperty("id").GetGuid();
        var createEtag = Assert.Single(createResponse.Headers.ETag is null ? [] : new[] { createResponse.Headers.ETag.Tag });
        Assert.Equal("idp-alice", created.RootElement.GetProperty("externalId").GetString());

        var filteredResponse = await client.GetAsync("/scim/v2/Users?filter=externalId%20eq%20%22idp-alice%22");
        filteredResponse.EnsureSuccessStatusCode();
        using (var filtered = await AssertScimResponseAsync(filteredResponse, "urn:ietf:params:scim:api:messages:2.0:ListResponse"))
        {
            Assert.Equal(1, filtered.RootElement.GetProperty("totalResults").GetInt32());
            Assert.Equal(userId, filtered.RootElement.GetProperty("resources")[0].GetProperty("id").GetGuid());
        }

        var disableResponse = await PatchAsync(client, userId, createEtag,
            """{"schemas":["urn:ietf:params:scim:api:messages:2.0:PatchOp"],"Operations":[{"op":"replace","path":"active","value":false}]}""");
        disableResponse.EnsureSuccessStatusCode();
        using var disabled = await AssertScimResponseAsync(disableResponse, "urn:ietf:params:scim:schemas:core:2.0:User");
        Assert.False(disabled.RootElement.GetProperty("active").GetBoolean());

        var enableEtag = disableResponse.Headers.ETag!.Tag;
        var objectPatchResponse = await PatchAsync(client, userId, enableEtag,
            """{"schemas":["urn:ietf:params:scim:api:messages:2.0:PatchOp"],"Operations":[{"op":"replace","value":{"active":true,"title":"Engineer","name":{"givenName":"Alicia"}}}]}""");
        objectPatchResponse.EnsureSuccessStatusCode();
        using var patched = await AssertScimResponseAsync(objectPatchResponse, "urn:ietf:params:scim:schemas:core:2.0:User");
        Assert.True(patched.RootElement.GetProperty("active").GetBoolean());
        Assert.Equal("Engineer", patched.RootElement.GetProperty("title").GetString());
        Assert.Equal("Alicia", patched.RootElement.GetProperty("name").GetProperty("givenName").GetString());

        var replaceRequest = """{"schemas":["urn:ietf:params:scim:schemas:core:2.0:User"],"userName":"alice.renamed","externalId":"idp-alice","active":true,"name":{"givenName":"Alice","familyName":"Renamed"},"emails":[{"value":"alice.renamed@example.com","primary":true}]}""";
        using var replaceMessage = ScimRequest(HttpMethod.Put, $"/scim/v2/Users/{userId}", replaceRequest, objectPatchResponse.Headers.ETag!.Tag);
        var replaceResponse = await client.SendAsync(replaceMessage);
        replaceResponse.EnsureSuccessStatusCode();
        using var replaced = await AssertScimResponseAsync(replaceResponse, "urn:ietf:params:scim:schemas:core:2.0:User");
        Assert.Equal("alice.renamed", replaced.RootElement.GetProperty("userName").GetString());
        Assert.Equal("alice.renamed@example.com", replaced.RootElement.GetProperty("emails")[0].GetProperty("value").GetString());

        using var deleteMessage = ScimRequest(HttpMethod.Delete, $"/scim/v2/Users/{userId}", null, replaceResponse.Headers.ETag!.Tag);
        var deleteResponse = await client.SendAsync(deleteMessage);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var missingResponse = await client.GetAsync($"/scim/v2/Users/{userId}");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Empty(factory.Users.Users);
    }

    [Fact]
    public async Task Duplicate_external_id_returns_a_scim_uniqueness_error()
    {
        using var client = factory.CreateClient();
        UseBearer(client, ReadWriteToken);
        (await PostUserAsync(client, "first", "shared-external")).EnsureSuccessStatusCode();

        var duplicate = await PostUserAsync(client, "second", "shared-external");

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var document = await AssertScimResponseAsync(duplicate, "urn:ietf:params:scim:api:messages:2.0:Error");
        Assert.Equal("uniqueness", document.RootElement.GetProperty("scimType").GetString());
        Assert.Single(factory.Users.Users);
    }

    [Fact]
    public async Task Stale_etag_rejects_patch_without_changing_the_user()
    {
        using var client = factory.CreateClient();
        UseBearer(client, ReadWriteToken);
        var createResponse = await PostUserAsync(client, "etag.user", "etag-external");
        using var created = await AssertScimResponseAsync(createResponse, "urn:ietf:params:scim:schemas:core:2.0:User");
        var userId = created.RootElement.GetProperty("id").GetGuid();

        var response = await PatchAsync(client, userId, "\"1\"",
            """{"schemas":["urn:ietf:params:scim:api:messages:2.0:PatchOp"],"Operations":[{"op":"replace","path":"active","value":false}]}""");

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        using var document = await AssertScimResponseAsync(response, "urn:ietf:params:scim:api:messages:2.0:Error");
        Assert.Contains("ETag", document.RootElement.GetProperty("detail").GetString());
        Assert.True(Assert.Single(factory.Users.Users).IsActive);
    }

    private static async Task<HttpResponseMessage> PostUserAsync(HttpClient client, string userName, string externalId, string? email = null)
    {
        var emailJson = email is null ? "[]" : $$"""[{"value":"{{email}}","primary":true}]""";
        var body = $$"""{"schemas":["urn:ietf:params:scim:schemas:core:2.0:User"],"userName":"{{userName}}","externalId":"{{externalId}}","active":true,"name":{"givenName":"Alice","familyName":"Example"},"emails":{{emailJson}}}""";
        using var request = ScimRequest(HttpMethod.Post, "/scim/v2/Users", body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PatchAsync(HttpClient client, Guid userId, string etag, string body)
    {
        using var request = ScimRequest(HttpMethod.Patch, $"/scim/v2/Users/{userId}", body, etag);
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage ScimRequest(HttpMethod method, string path, string? body, string? etag = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/scim+json");
        if (etag is not null)
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        return request;
    }

    private static void UseBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<JsonDocument> AssertScimResponseAsync(HttpResponseMessage response, string schema)
    {
        Assert.Equal("application/scim+json", response.Content.Headers.ContentType?.MediaType);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(schema, document.RootElement.GetProperty("schemas").EnumerateArray().Select(item => item.GetString()));
        return document;
    }
}

public sealed class ScimProvisioningFactory : WebApplicationFactory<AppWebModule>
{
    public InMemoryScimUserRepository Users { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development")
            .UseSetting("BootstrapAdmin:Username", string.Empty)
            .UseSetting("BootstrapAdmin:Password", string.Empty)
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.RemoveAll<IRefreshTokenRepository>();
                services.RemoveAll<ISsoSessionService>();
                services.RemoveAll<IScimCredentialService>();
                services.RemoveAll<IScimUserService>();
                services.AddSingleton(Users);
                services.AddSingleton<IUserRepository>(provider => provider.GetRequiredService<InMemoryScimUserRepository>());
                services.AddSingleton(NoOpProxy.Create<IRefreshTokenRepository>());
                services.AddSingleton<ISsoSessionService, NoOpSsoSessionService>();
                services.AddSingleton<IScimCredentialService, TestScimCredentialService>();
                services.AddScoped<IScimUserService, ScimUserService>();
            });
    }
}

public sealed class InMemoryScimUserRepository : IUserRepository
{
    private readonly List<User> users = [];
    private readonly object gate = new();
    private readonly HashSet<Guid> pendingPrecisionRoundTrip = [];

    public IReadOnlyList<User> Users
    {
        get { lock (gate) return users.ToArray(); }
    }

    public void Reset()
    {
        lock (gate)
        {
            users.Clear();
            pendingPrecisionRoundTrip.Clear();
        }
    }

    public Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default) =>
        Task.FromResult<User?>(Snapshot().FirstOrDefault(user => user.Username == username));

    public Task<User?> FindByExternalIdAsync(string externalId, CancellationToken ct = default) =>
        Task.FromResult<User?>(Snapshot().FirstOrDefault(user => user.ExternalId == externalId.Trim()));

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        Task.FromResult<User?>(Snapshot().FirstOrDefault(user => user.Email == email.ToLowerInvariant()));

    public Task<User?> FindByPhoneNumberAsync(string phoneNumber, CancellationToken ct = default) =>
        Task.FromResult<User?>(Snapshot().FirstOrDefault(user => user.PhoneNumber == phoneNumber));

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        lock (gate)
        {
            var user = users.FirstOrDefault(item => item.Id == id);
            if (user is not null && pendingPrecisionRoundTrip.Remove(id))
                TruncateUpdatedAtToPostgresPrecision(user);
            return Task.FromResult<User?>(user);
        }
    }

    public Task<(IReadOnlyList<User> Items, int Total)> GetScimPagedAsync(string? userName, string? externalId,
        bool? isActive, string? email, int startIndex, int count, CancellationToken ct = default)
    {
        var query = Snapshot().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(userName)) query = query.Where(user => user.Username == userName);
        if (!string.IsNullOrWhiteSpace(externalId)) query = query.Where(user => user.ExternalId == externalId);
        if (isActive.HasValue) query = query.Where(user => user.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(email)) query = query.Where(user => user.Email == email.ToLowerInvariant());
        var filtered = query.ToArray();
        return Task.FromResult(((IReadOnlyList<User>)filtered.Skip(startIndex - 1).Take(count).ToArray(), filtered.Length));
    }

    public Task<(IReadOnlyList<User> Items, int Total)> GetAdminPagedAsync(string? keyword, bool? isActive,
        int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        lock (gate)
        {
            users.Add(user);
            pendingPrecisionRoundTrip.Add(user.Id);
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;

    public Task DeleteAsync(User user, CancellationToken ct = default)
    {
        lock (gate) users.Remove(user);
        return Task.CompletedTask;
    }

    public Task<User?> RegisterFailedLoginAsync(Guid userId, int failureLimit, TimeSpan lockoutDuration,
        DateTimeOffset now, CancellationToken ct = default) => throw new NotSupportedException();

    public Task ResetLoginFailuresAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public User? Find(Guid primaryKey) => Snapshot().FirstOrDefault(user => user.Id == primaryKey);
    public ValueTask<User?> FindAsync(Guid primaryKey) => ValueTask.FromResult(Find(primaryKey));
    public Task<User?> FindAsync(Expression<Func<User, bool>> predicate) =>
        Task.FromResult(Snapshot().AsQueryable().FirstOrDefault(predicate));
    public IQueryable<User> FindAll() => Snapshot().AsQueryable();
    public IQueryable<User> FindAll(Expression<Func<User, bool>> predicate) => Snapshot().AsQueryable().Where(predicate);
    public void Attach(User entity) { }
    public void Add(User entity) => AddAsync(entity).GetAwaiter().GetResult();
    public void Update(User entity) { }
    public void Remove(User entity) => DeleteAsync(entity).GetAwaiter().GetResult();

    private User[] Snapshot()
    {
        lock (gate) return users.ToArray();
    }

    private static void TruncateUpdatedAtToPostgresPrecision(User user)
    {
        var ticks = user.UpdatedAt.UtcTicks - user.UpdatedAt.UtcTicks % TimeSpan.TicksPerMicrosecond;
        typeof(User).GetProperty(nameof(User.UpdatedAt))!.SetValue(user, new DateTimeOffset(ticks, TimeSpan.Zero));
    }
}

public sealed class TestScimCredentialService : IScimCredentialService
{
    public Task<bool> ValidateAsync(string rawToken, string requiredScope, CancellationToken ct = default) =>
        Task.FromResult(rawToken == "integration-read-write-token" ||
            rawToken == "integration-read-only-token" && requiredScope == "scim:read");

    public Task<ScimCredentialCreated> CreateAsync(string name, IReadOnlyCollection<string>? scopes,
        DateTimeOffset? expiresAt, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<ScimCredentialSummary>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ScimCredentialSummary?> UpdateAsync(Guid id, string name, IReadOnlyCollection<string>? scopes,
        DateTimeOffset? expiresAt, bool isActive, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> RevokeAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
}

public sealed class NoOpSsoSessionService : ISsoSessionService
{
    public Task<Guid> CreateAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(Guid.NewGuid());
    public Task<bool> IsActiveAsync(Guid sessionId, Guid userId, CancellationToken ct = default) => Task.FromResult(true);
    public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;
}

public class NoOpProxy : DispatchProxy
{
    public static T Create<T>() where T : class => DispatchProxy.Create<T, NoOpProxy>();

    protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.ReturnType == typeof(Task)) return Task.CompletedTask;
        if (targetMethod?.ReturnType == typeof(ValueTask)) return ValueTask.CompletedTask;
        if (targetMethod?.ReturnType.IsGenericType == true && targetMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = targetMethod.ReturnType.GetGenericArguments()[0];
            var result = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, [result]);
        }
        throw new NotSupportedException(targetMethod?.Name);
    }
}
