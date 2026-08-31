using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Sdk;

namespace NexusAuth.Host.IntegrationTests;

public sealed class ScimContainerApiTests
{
    private static readonly Uri ContainerScimBaseAddress = new("http://localhost:5100/scim/v2/");
    private const string ReadOnlyToken = "nexusauth-local-scim-read-token-v1";
    private const string ReadWriteToken = "nexusauth-local-scim-read-write-token-v1";
    private const string UserSchema = "urn:ietf:params:scim:schemas:core:2.0:User";
    private const string ListSchema = "urn:ietf:params:scim:api:messages:2.0:ListResponse";
    private const string ErrorSchema = "urn:ietf:params:scim:api:messages:2.0:Error";

    [Fact]
    [Trait("Category", "Container")]
    public async Task Deployed_host_rejects_an_unauthenticated_scim_request()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("ServiceProviderConfig");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var document = await AssertScimResponseAsync(response, ErrorSchema);
        Assert.Equal("invalidToken", document.RootElement.GetProperty("scimType").GetString());
    }

    [Fact]
    [Trait("Category", "Container")]
    public async Task Deployed_host_rejects_read_only_credentials_before_model_binding()
    {
        using var client = CreateClient(ReadOnlyToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "Users")
        {
            Content = new StringContent("{", Encoding.UTF8, "application/scim+json")
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = await AssertScimResponseAsync(response, ErrorSchema);
        Assert.Equal("forbidden", document.RootElement.GetProperty("scimType").GetString());
    }

    [Fact]
    [Trait("Category", "Container")]
    public async Task Deployed_host_supports_the_complete_scim_user_lifecycle()
    {
        using var client = CreateClient(ReadWriteToken);
        var runId = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Environment.ProcessId}-{RandomNumberGenerator.GetInt32(100000)}";
        var userName = $"scim.container.{runId}";
        var externalId = $"scim-container-external-{runId}";
        var initialEmail = $"{userName}@example.test";
        var updatedEmail = $"{userName}.updated@example.test";
        Guid? userId = null;

        try
        {
            using (var discoveryResponse = await client.GetAsync("ServiceProviderConfig"))
            {
                discoveryResponse.EnsureSuccessStatusCode();
                using var discovery = await AssertScimResponseAsync(
                    discoveryResponse,
                    "urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig");
                Assert.True(discovery.RootElement.GetProperty("patch").GetProperty("supported").GetBoolean());
                Assert.True(discovery.RootElement.GetProperty("filter").GetProperty("supported").GetBoolean());
            }

            var createBody = JsonSerializer.Serialize(new
            {
                schemas = new[] { UserSchema },
                userName,
                externalId,
                active = true,
                name = new { givenName = "Container", familyName = "Test" },
                emails = new[] { new { value = initialEmail, primary = true } }
            });
            using var createResponse = await SendScimAsync(client, HttpMethod.Post, "Users", createBody);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            using var created = await AssertScimResponseAsync(createResponse, UserSchema);
            userId = created.RootElement.GetProperty("id").GetGuid();
            var createEtag = RequiredEtag(createResponse);
            Assert.Equal(externalId, created.RootElement.GetProperty("externalId").GetString());
            Assert.Equal(initialEmail, created.RootElement.GetProperty("emails")[0].GetProperty("value").GetString());

            var filter = Uri.EscapeDataString($"externalId eq \"{externalId}\"");
            using (var filterResponse = await client.GetAsync($"Users?filter={filter}"))
            {
                filterResponse.EnsureSuccessStatusCode();
                using var filtered = await AssertScimResponseAsync(filterResponse, ListSchema);
                Assert.Equal(1, filtered.RootElement.GetProperty("totalResults").GetInt32());
                Assert.Equal(userId, filtered.RootElement.GetProperty("resources")[0].GetProperty("id").GetGuid());
            }

            const string disableBody = """
                {"schemas":["urn:ietf:params:scim:api:messages:2.0:PatchOp"],"Operations":[{"op":"replace","path":"active","value":false}]}
                """;
            using var disableResponse = await SendScimAsync(
                client,
                HttpMethod.Patch,
                $"Users/{userId}",
                disableBody,
                createEtag);
            disableResponse.EnsureSuccessStatusCode();
            using var disabled = await AssertScimResponseAsync(disableResponse, UserSchema);
            Assert.False(disabled.RootElement.GetProperty("active").GetBoolean());
            var disabledEtag = RequiredEtag(disableResponse);

            const string objectPatchBody = """
                {"schemas":["urn:ietf:params:scim:api:messages:2.0:PatchOp"],"Operations":[{"op":"replace","value":{"active":true,"title":"Container Engineer","name":{"givenName":"Updated"}}}]}
                """;
            using var objectPatchResponse = await SendScimAsync(
                client,
                HttpMethod.Patch,
                $"Users/{userId}",
                objectPatchBody,
                disabledEtag);
            objectPatchResponse.EnsureSuccessStatusCode();
            using var patched = await AssertScimResponseAsync(objectPatchResponse, UserSchema);
            Assert.True(patched.RootElement.GetProperty("active").GetBoolean());
            Assert.Equal("Container Engineer", patched.RootElement.GetProperty("title").GetString());
            Assert.Equal("Updated", patched.RootElement.GetProperty("name").GetProperty("givenName").GetString());
            var patchedEtag = RequiredEtag(objectPatchResponse);

            using (var staleResponse = await SendScimAsync(
                client,
                HttpMethod.Patch,
                $"Users/{userId}",
                disableBody,
                "\"1\""))
            {
                Assert.Equal(HttpStatusCode.PreconditionFailed, staleResponse.StatusCode);
                using var staleError = await AssertScimResponseAsync(staleResponse, ErrorSchema);
                Assert.Contains("ETag", staleError.RootElement.GetProperty("detail").GetString());
            }

            var replaceBody = JsonSerializer.Serialize(new
            {
                schemas = new[] { UserSchema },
                userName = $"{userName}.updated",
                externalId,
                active = true,
                name = new { givenName = "Container", familyName = "Updated" },
                emails = new[] { new { value = updatedEmail, primary = true } }
            });
            using var replaceResponse = await SendScimAsync(
                client,
                HttpMethod.Put,
                $"Users/{userId}",
                replaceBody,
                patchedEtag);
            replaceResponse.EnsureSuccessStatusCode();
            using var replaced = await AssertScimResponseAsync(replaceResponse, UserSchema);
            Assert.Equal($"{userName}.updated", replaced.RootElement.GetProperty("userName").GetString());
            Assert.Equal(updatedEmail, replaced.RootElement.GetProperty("emails")[0].GetProperty("value").GetString());

            var deletedUserId = userId.Value;
            using var deleteResponse = await SendScimAsync(
                client,
                HttpMethod.Delete,
                $"Users/{deletedUserId}",
                etag: RequiredEtag(replaceResponse));
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
            userId = null;

            using var missingResponse = await client.GetAsync($"Users/{deletedUserId}");
            Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
            await AssertScimResponseAsync(missingResponse, ErrorSchema);
        }
        finally
        {
            if (userId.HasValue)
            {
                using var cleanupResponse = await SendScimAsync(
                    client,
                    HttpMethod.Delete,
                    $"Users/{userId.Value}");
            }
        }
    }

    private static HttpClient CreateClient(string? bearerToken = null)
    {
        var client = new HttpClient { BaseAddress = ContainerScimBaseAddress };
        if (bearerToken is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return client;
    }

    private static async Task<HttpResponseMessage> SendScimAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string? body = null,
        string? etag = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/scim+json");
        if (etag is not null)
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        return await client.SendAsync(request);
    }

    private static async Task<JsonDocument> AssertScimResponseAsync(HttpResponseMessage response, string schema)
    {
        Assert.Equal("application/scim+json", response.Content.Headers.ContentType?.MediaType);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(schema, document.RootElement.GetProperty("schemas").EnumerateArray().Select(item => item.GetString()));
        return document;
    }

    private static string RequiredEtag(HttpResponseMessage response)
    {
        return response.Headers.ETag?.Tag ?? throw new XunitException("SCIM response did not include an ETag.");
    }

}
