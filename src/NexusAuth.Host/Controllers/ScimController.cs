using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using NexusAuth.Application.Services.Scim;

namespace NexusAuth.Host.Controllers;

[ApiController]
[Route("scim/v2")]
public sealed class ScimController(IScimUserService scimUserService, IScimCredentialService credentialService) : ControllerBase
{
    private const string ScimContentType = "application/scim+json";
    private const string UserSchema = "urn:ietf:params:scim:schemas:core:2.0:User";
    private const string ListSchema = "urn:ietf:params:scim:api:messages:2.0:ListResponse";
    private const string ErrorSchema = "urn:ietf:params:scim:api:messages:2.0:Error";

    [HttpGet("ServiceProviderConfig")]
    public async Task<IActionResult> ServiceProviderConfig(CancellationToken ct) => await AuthorizeAsync("scim:read", ct)
        ? Scim(new
        {
            schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig" },
            patch = new { supported = true }, bulk = new { supported = false, maxOperations = 0, maxPayloadSize = 0 },
            filter = new { supported = true, maxResults = 200 }, changePassword = new { supported = false },
            sort = new { supported = false }, etag = new { supported = true }, authenticationSchemes = new[]
            {
                new { type = "oauthbearertoken", name = "Bearer Token", description = "SCIM service-principal bearer token.", specUri = "https://www.rfc-editor.org/rfc/rfc6750" }
            }
        }) : ScimError(401, "invalidToken", "A valid SCIM bearer token is required.");

    [HttpGet("ResourceTypes")]
    public async Task<IActionResult> ResourceTypes(CancellationToken ct) => await AuthorizeAsync("scim:read", ct)
        ? Scim(new { schemas = new[] { ListSchema }, totalResults = 1, startIndex = 1, itemsPerPage = 1, Resources = new[] { UserResourceType() } })
        : ScimError(401, "invalidToken", "A valid SCIM bearer token is required.");

    [HttpGet("Schemas")]
    public async Task<IActionResult> Schemas(CancellationToken ct) => await AuthorizeAsync("scim:read", ct)
        ? Scim(new { schemas = new[] { ListSchema }, totalResults = 1, startIndex = 1, itemsPerPage = 1, Resources = new[] { UserSchemaDefinition() } })
        : ScimError(401, "invalidToken", "A valid SCIM bearer token is required.");

    [HttpGet("Schemas/{id}")]
    public async Task<IActionResult> Schema(string id, CancellationToken ct)
    {
        if (!await AuthorizeAsync("scim:read", ct)) return ScimError(401, "invalidToken", "A valid SCIM bearer token is required.");
        return string.Equals(id, UserSchema, StringComparison.Ordinal) ? Scim(UserSchemaDefinition()) : ScimError(404, null, "Schema was not found.");
    }

    [HttpGet("Users")]
    public async Task<IActionResult> ListUsers([FromQuery] string? filter, [FromQuery] int startIndex = 1, [FromQuery] int count = 100, CancellationToken ct = default)
    {
        if (!await AuthorizeAsync("scim:read", ct)) return ScimError(401, "invalidToken", "A valid SCIM bearer token is required.");
        try
        {
            var list = await scimUserService.ListAsync(filter, startIndex, count, ct);
            return Scim(new { schemas = new[] { ListSchema }, totalResults = list.TotalResults, startIndex = list.StartIndex, itemsPerPage = list.Resources.Count, Resources = list.Resources.Select(ToResponse) });
        }
        catch (ArgumentException exception) { return ScimError(400, "invalidFilter", exception.Message); }
    }

    [HttpGet("Users/{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken ct)
    {
        if (!await AuthorizeAsync("scim:read", ct)) return ScimError(401, "invalidToken", "A valid SCIM bearer token is required.");
        var user = await scimUserService.FindAsync(id, ct);
        return user is null ? ScimError(404, null, "User was not found.") : ScimUser(user);
    }

    [HttpPost("Users")]
    public async Task<IActionResult> CreateUser([FromBody] ScimUserRequest request, CancellationToken ct)
    {
        if (!await AuthorizeAsync("scim:write", ct)) return ScimError(403, "forbidden", "The credential does not grant SCIM write access.");
        try
        {
            EnsureSinglePrimaryValue(request.Emails, "emails");
            EnsureSinglePrimaryValue(request.PhoneNumbers, "phoneNumbers");
            var user = await scimUserService.CreateAsync(ToInput(request), ct);
            Response.Headers.Location = $"{Request.Scheme}://{Request.Host}/scim/v2/Users/{user.Id}";
            return Scim(ToResponse(user), 201, user);
        }
        catch (ArgumentException exception) { return ScimError(400, "invalidValue", exception.Message); }
        catch (InvalidOperationException exception) { return ScimError(409, "uniqueness", exception.Message); }
    }

    [HttpPut("Users/{id:guid}")]
    public async Task<IActionResult> ReplaceUser(Guid id, [FromBody] ScimUserRequest request, CancellationToken ct)
    {
        if (!await AuthorizeAsync("scim:write", ct)) return ScimError(403, "forbidden", "The credential does not grant SCIM write access.");
        try
        {
            EnsureSinglePrimaryValue(request.Emails, "emails");
            EnsureSinglePrimaryValue(request.PhoneNumbers, "phoneNumbers");
            var user = await scimUserService.ReplaceAsync(id, ToInput(request), Request.Headers.IfMatch, ct);
            return user is null ? ScimError(412, null, "User was not found or the ETag does not match.") : ScimUser(user);
        }
        catch (ArgumentException exception) { return ScimError(400, "invalidValue", exception.Message); }
        catch (InvalidOperationException exception) { return ScimError(409, "uniqueness", exception.Message); }
    }

    [HttpPatch("Users/{id:guid}")]
    public async Task<IActionResult> PatchUser(Guid id, [FromBody] ScimPatchRequest request, CancellationToken ct)
    {
        if (!await AuthorizeAsync("scim:write", ct)) return ScimError(403, "forbidden", "The credential does not grant SCIM write access.");
        try
        {
            var operations = request.Operations?.Select(operation => new ScimPatchOperation(operation.Op ?? "", operation.Path, StringValue(operation.Value))).ToArray() ?? [];
            if (operations.Length == 0) return ScimError(400, "invalidValue", "PATCH Operations is required.");
            var user = await scimUserService.PatchAsync(id, operations, Request.Headers.IfMatch, ct);
            return user is null ? ScimError(412, null, "User was not found or the ETag does not match.") : ScimUser(user);
        }
        catch (ArgumentException exception) { return ScimError(400, "invalidValue", exception.Message); }
        catch (InvalidOperationException exception) { return ScimError(409, "uniqueness", exception.Message); }
    }

    [HttpDelete("Users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        if (!await AuthorizeAsync("scim:write", ct)) return ScimError(403, "forbidden", "The credential does not grant SCIM write access.");
        return await scimUserService.DeleteAsync(id, Request.Headers.IfMatch, ct) ? NoContent() : ScimError(412, null, "User was not found or the ETag does not match.");
    }

    private async Task<bool> AuthorizeAsync(string requiredScope, CancellationToken ct)
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        return await credentialService.ValidateAsync(authorization[7..].Trim(), requiredScope, ct);
    }

    private IActionResult ScimUser(ScimUser user) => Scim(ToResponse(user), 200, user);
    private IActionResult Scim(object value, int status = 200, ScimUser? user = null)
    {
        if (user is not null) Response.Headers.ETag = ScimUserService.Version(user);
        return new JsonResult(value) { StatusCode = status, ContentType = ScimContentType };
    }
    private IActionResult ScimError(int status, string? scimType, string detail) => Scim(new { schemas = new[] { ErrorSchema }, status = status.ToString(), scimType, detail }, status);

    private object ToResponse(ScimUser user) => new
    {
        schemas = new[] { UserSchema }, id = user.Id.ToString(), externalId = user.ExternalId, userName = user.UserName,
        name = new { formatted = FormatName(user), familyName = user.FamilyName, givenName = user.GivenName, middleName = user.MiddleName, honorificPrefix = user.HonorificPrefix, honorificSuffix = user.HonorificSuffix },
        profileUrl = user.ProfileUrl, title = user.Title, userType = user.UserType, preferredLanguage = user.PreferredLanguage, locale = user.Locale, timezone = user.Timezone, active = user.Active,
        emails = user.Email is null ? Array.Empty<object>() : new object[] { new { value = user.Email, type = "work", primary = true } },
        phoneNumbers = user.PhoneNumber is null ? Array.Empty<object>() : new object[] { new { value = user.PhoneNumber, type = "work", primary = true } },
        meta = new { resourceType = "User", created = user.CreatedAt, lastModified = user.UpdatedAt, location = $"{Request.Scheme}://{Request.Host}/scim/v2/Users/{user.Id}", version = ScimUserService.Version(user) }
    };

    private static ScimUserInput ToInput(ScimUserRequest request) => new(request.UserName, request.ExternalId, request.Active, FirstValue(request.Emails), FirstValue(request.PhoneNumbers), request.Name?.GivenName, request.Name?.FamilyName, request.Name?.MiddleName, request.Name?.HonorificPrefix, request.Name?.HonorificSuffix, request.ProfileUrl, request.Title, request.UserType, request.PreferredLanguage, request.Locale, request.Timezone);
    private static string? FirstValue(IReadOnlyList<ScimValue>? values) => values?.FirstOrDefault(value => value.Primary == true)?.Value ?? values?.FirstOrDefault()?.Value;
    private static void EnsureSinglePrimaryValue(IReadOnlyList<ScimValue>? values, string attribute)
    {
        if (values is { Count: > 1 })
            throw new ArgumentException($"{attribute} currently supports one primary value.");
    }
    private static string? StringValue(JsonElement? value) => value is null || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : value.Value.GetRawText();
    private static string FormatName(ScimUser user) => string.Join(" ", new[] { user.HonorificPrefix, user.GivenName, user.MiddleName, user.FamilyName, user.HonorificSuffix }.Where(value => !string.IsNullOrWhiteSpace(value)));
    private static object UserResourceType() => new { schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:ResourceType" }, id = "User", name = "User", endpoint = "/Users", schema = UserSchema, description = "User Account" };
    private static object UserSchemaDefinition() => new { schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:Schema" }, id = UserSchema, name = "User", description = "User Account", attributes = new object[] { new { name = "userName", type = "string", multiValued = false, required = true, mutability = "readWrite", returned = "default", uniqueness = "server" }, new { name = "externalId", type = "string", multiValued = false, required = false, mutability = "readWrite", returned = "default", uniqueness = "none" }, new { name = "active", type = "boolean", multiValued = false, required = false, mutability = "readWrite", returned = "default", uniqueness = "none" }, new { name = "name", type = "complex", multiValued = false, required = false, mutability = "readWrite", returned = "default", uniqueness = "none" }, new { name = "emails", type = "complex", multiValued = true, required = false, mutability = "readWrite", returned = "default", uniqueness = "none" }, new { name = "phoneNumbers", type = "complex", multiValued = true, required = false, mutability = "readWrite", returned = "default", uniqueness = "none" } } };
}

public sealed class ScimUserRequest
{
    [JsonPropertyName("userName")] public string? UserName { get; init; }
    [JsonPropertyName("externalId")] public string? ExternalId { get; init; }
    [JsonPropertyName("active")] public bool? Active { get; init; }
    [JsonPropertyName("name")] public ScimName? Name { get; init; }
    [JsonPropertyName("emails")] public IReadOnlyList<ScimValue>? Emails { get; init; }
    [JsonPropertyName("phoneNumbers")] public IReadOnlyList<ScimValue>? PhoneNumbers { get; init; }
    [JsonPropertyName("profileUrl")] public string? ProfileUrl { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("userType")] public string? UserType { get; init; }
    [JsonPropertyName("preferredLanguage")] public string? PreferredLanguage { get; init; }
    [JsonPropertyName("locale")] public string? Locale { get; init; }
    [JsonPropertyName("timezone")] public string? Timezone { get; init; }
}
public sealed class ScimName { [JsonPropertyName("givenName")] public string? GivenName { get; init; } [JsonPropertyName("familyName")] public string? FamilyName { get; init; } [JsonPropertyName("middleName")] public string? MiddleName { get; init; } [JsonPropertyName("honorificPrefix")] public string? HonorificPrefix { get; init; } [JsonPropertyName("honorificSuffix")] public string? HonorificSuffix { get; init; } }
public sealed class ScimValue { [JsonPropertyName("value")] public string? Value { get; init; } [JsonPropertyName("primary")] public bool? Primary { get; init; } }
public sealed class ScimPatchRequest { [JsonPropertyName("Operations")] public IReadOnlyList<ScimPatchOperationRequest>? Operations { get; init; } }
public sealed class ScimPatchOperationRequest { [JsonPropertyName("op")] public string? Op { get; init; } [JsonPropertyName("path")] public string? Path { get; init; } [JsonPropertyName("value")] public JsonElement? Value { get; init; } }
