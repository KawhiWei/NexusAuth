using Microsoft.AspNetCore.Mvc;
using NexusAuth.Application.Services.ApiResources;
using NexusAuth.Domain.AggregateRoots.ApiResources;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Workbench.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApiResourcesController : ControllerBase
{
    private readonly IApiResourceService _apiResourceService;
    private readonly IApiResourceRepository _apiResourceRepository;

    public ApiResourcesController(
        IApiResourceService apiResourceService,
        IApiResourceRepository apiResourceRepository)
    {
        _apiResourceService = apiResourceService;
        _apiResourceRepository = apiResourceRepository;
    }

    [HttpGet]
    public async Task<List<ApiResource>> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        CancellationToken ct = default)
    {
        var resources = await _apiResourceRepository.GetAllActiveAsync(ct);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            resources = resources
                .Where(r => r.Name.ToLower().Contains(kw) || r.DisplayName.ToLower().Contains(kw))
                .ToList();
        }

        if (isActive.HasValue)
        {
            resources = resources.Where(r => r.IsActive == isActive.Value).ToList();
        }

        return resources.ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ApiResource?> GetById(Guid id, CancellationToken ct = default)
    {
        return await _apiResourceRepository.FindByIdAsync(id, ct);
    }

    [HttpPost]
    public async Task<ApiResource> Create([FromBody] CreateApiResourceRequest request, CancellationToken ct = default)
    {
        return await _apiResourceService.RegisterAsync(
            request.Name,
            request.DisplayName,
            request.Audience,
            request.Description,
            ct);
    }
}

public record CreateApiResourceRequest(
    string Name,
    string DisplayName,
    string Audience,
    string? Description);