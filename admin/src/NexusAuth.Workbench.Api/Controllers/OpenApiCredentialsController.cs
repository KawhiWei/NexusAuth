using Luck.AspNetCore.ApiResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusAuth.Application.Services.OpenApi;

namespace NexusAuth.Workbench.Api.Controllers;

/// <summary>Administrative lifecycle management for Host Open API bearer credentials.</summary>
[Authorize]
[ApiController]
[ApiResultWrap]
[Route("api/open-api-credentials")]
public sealed class OpenApiCredentialsController(IOpenApiCredentialService credentialService) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<OpenApiCredentialSummary>> GetAll(CancellationToken ct = default) => credentialService.GetAllAsync(ct);

    [HttpPost]
    public Task<OpenApiCredentialCreated> Create([FromBody] CreateOpenApiCredentialRequest request, CancellationToken ct = default) =>
        credentialService.CreateAsync(request.Name, request.TargetType, request.ExpiresAt, ct);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOpenApiCredentialRequest request, CancellationToken ct = default)
    {
        var credential = await credentialService.UpdateAsync(id, request.Name, request.ExpiresAt, request.IsActive, ct);
        return credential is null ? NotFound() : Ok(credential);
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct = default) =>
        await credentialService.RevokeAsync(id, ct) ? NoContent() : NotFound();
}

public sealed record CreateOpenApiCredentialRequest(string Name, string TargetType, DateTimeOffset? ExpiresAt);
public sealed record UpdateOpenApiCredentialRequest(string Name, DateTimeOffset? ExpiresAt, bool IsActive);
