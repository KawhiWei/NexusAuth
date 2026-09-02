using Microsoft.AspNetCore.Mvc;
using NexusAuth.Application.Clients;
using NexusAuth.Application.Services.ApiResources;
using NexusAuth.Domain.Entities;
using NexusAuth.Host.Filters;

namespace NexusAuth.Host.Controllers;

[ApiController]
[Route("openapi/v1")]
public sealed class OpenApiController(IClientService clientService, IApiResourceService apiResourceService) : ControllerBase
{
    [HttpGet("applications")]
    [OpenApiAuthorize(OpenApiCredential.TargetTypeApplication)]
    public async Task<IReadOnlyList<OpenApiApplicationDto>> GetApplications(
        [FromQuery] string? keyword,
        CancellationToken ct = default)
    {
        var clients = await clientService.GetAllAsync(keyword, isActive: true, ct);
        return clients.Select(client => new OpenApiApplicationDto(
            client.Id,
            client.ClientId,
            client.ClientName,
            client.Description,
            client.IsActive,
            client.CreatedAt)).ToArray();
    }

    [HttpGet("service-resources")]
    [OpenApiAuthorize(OpenApiCredential.TargetTypeServiceResource)]
    public async Task<IReadOnlyList<OpenApiServiceResourceDto>> GetServiceResources(
        [FromQuery] string? keyword,
        CancellationToken ct = default)
    {
        var resources = await apiResourceService.GetAllAsync(keyword, isActive: true, ct);
        return resources.Select(resource => new OpenApiServiceResourceDto(
            resource.Id,
            resource.Name,
            resource.DisplayName,
            resource.Audience,
            resource.Description,
            resource.IsActive,
            resource.CreatedAt)).ToArray();
    }
}

public sealed record OpenApiApplicationDto(Guid Id, string ClientId, string ClientName, string? Description, bool IsActive, DateTimeOffset CreatedAt);
public sealed record OpenApiServiceResourceDto(Guid Id, string Name, string DisplayName, string Audience, string? Description, bool IsActive, DateTimeOffset CreatedAt);
