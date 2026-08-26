using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Luck.AspNetCore.ApiResults;
using Luck.Framework.Exceptions;
using NexusAuth.Application;
using NexusAuth.Application.Services.ApiResources;

namespace NexusAuth.Workbench.Api.Controllers;

[Authorize]
[ApiController]
[ApiResultWrap]
[Route("api/api-resources")]
public class ApiResourcesController : ControllerBase
{
    private readonly IApiResourceService _apiResourceService;

    public ApiResourcesController(IApiResourceService apiResourceService)
    {
        _apiResourceService = apiResourceService;
    }

    [HttpGet("all")]
    public async Task<List<ApiResourceDto>> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        CancellationToken ct = default)
    {
        return await _apiResourceService.GetAllAsync(keyword, isActive, ct);
    }

    [HttpGet]
    public async Task<PagedResult<ApiResourceDto>> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        return await _apiResourceService.GetPagedAsync(keyword, isActive, page, pageSize, ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<ApiResourceDto> GetById(Guid id, CancellationToken ct = default)
    {
        var resource = await _apiResourceService.GetByIdAsync(id, ct);
        if (resource is null)
            throw new NotFoundException($"API resource with id '{id}' was not found.");

        return resource;
    }

    [HttpPost]
    public async Task<ApiResourceDto> Create(
        [FromBody] CreateApiResourceRequest request,
        CancellationToken ct = default)
    {
        return await _apiResourceService.CreateAsync(request, ct);
    }

    [HttpPut("{id:guid}")]
    public async Task<ApiResourceDto> Update(Guid id, [FromBody] UpdateApiResourceRequest request, CancellationToken ct = default)
    {
        if (await _apiResourceService.GetByIdAsync(id, ct) is null)
            throw new NotFoundException($"API resource with id '{id}' was not found.");

        return await _apiResourceService.UpdateAsync(id, request, ct);
    }

    [HttpDelete("{id:guid}")]
    public async Task Delete(Guid id, CancellationToken ct = default)
    {
        await _apiResourceService.DeleteAsync(id, ct);
    }
}
