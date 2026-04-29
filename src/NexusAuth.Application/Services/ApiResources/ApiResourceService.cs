using NexusAuth.Application;

namespace NexusAuth.Application.Services.ApiResources;

public class ApiResourceService(
    IApiResourceRepository apiResourceRepository,
    IClientApiResourceRepository clientApiResourceRepository) : IApiResourceService
{
    public async Task<List<ApiResourceDto>> GetAllAsync(string? keyword = null, bool? isActive = null, CancellationToken ct = default)
    {
        var (resources, _) = await apiResourceRepository.GetPagedAsync(keyword, isActive, 1, int.MaxValue, ct);
        return resources.Select(MapDto).ToList();
    }

    public async Task<PagedResult<ApiResourceDto>> GetPagedAsync(
        string? keyword = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var (resources, total) = await apiResourceRepository.GetPagedAsync(keyword, isActive, normalizedPage, normalizedPageSize, ct);
        return new PagedResult<ApiResourceDto>(resources.Select(MapDto).ToList(), total, normalizedPage, normalizedPageSize);
    }

    public async Task<ApiResourceDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var resource = await apiResourceRepository.FindByIdAsync(id, ct);
        return resource is null ? null : MapDto(resource);
    }

    /// <summary>
    /// 注册 API 资源，并指定�?audience�?
    /// </summary>
    
    public async Task<ApiResource> RegisterAsync(
        string name,
        string displayName,
        string audience,
        string? description = null,
        CancellationToken ct = default)
    {
        var existing = await apiResourceRepository.FindByNameAsync(name, ct);
        if (existing is not null)
            throw new InvalidOperationException($"API resource with name '{name}' already exists.");

        var resource = ApiResource.Create(name, displayName, audience, description);
        await apiResourceRepository.AddAsync(resource, ct);

        return resource;
    }

    public async Task<ApiResourceDto> CreateAsync(CreateApiResourceRequest request, CancellationToken ct = default)
    {
        var resource = await RegisterAsync(
            request.Name,
            request.DisplayName,
            request.Audience,
            request.Description,
            ct);

        return MapDto(resource);
    }

    public async Task<ApiResourceDto> UpdateAsync(Guid id, UpdateApiResourceRequest request, CancellationToken ct = default)
    {
        var resource = await apiResourceRepository.FindByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Api resource with id {id} not found.");

        resource.Update(
            request.DisplayName,
            request.Audience,
            request.Description,
            request.IsActive);

        await apiResourceRepository.UpdateAsync(resource, ct);
        return MapDto(resource);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var resource = await apiResourceRepository.FindByIdAsync(id, ct);
        if (resource is null)
            return;

        await clientApiResourceRepository.RemoveByApiResourceIdAsync(id, ct);
        await apiResourceRepository.DeleteAsync(resource, ct);
    }

    /// <summary>
    /// 将资源授权给指定客户端�?
    /// </summary>
    public async Task AssignToClientAsync(
        Guid clientId,
        Guid apiResourceId,
        CancellationToken ct = default)
    {
        var association = ClientApiResource.Create(clientId, apiResourceId);
        await clientApiResourceRepository.AddAsync(association, ct);
    }

    /// <summary>
    /// 撤销客户端对指定资源的授权�?
    /// </summary>
    public async Task RevokeFromClientAsync(
        Guid clientId,
        Guid apiResourceId,
        CancellationToken ct = default)
    {
        await clientApiResourceRepository.RemoveAsync(clientId, apiResourceId, ct);
    }

    /// <summary>
    /// 查询客户端当前可访问的资源列表�?
    /// </summary>
    public async Task<IReadOnlyList<ApiResource>> GetClientResourcesAsync(
        Guid clientId,
        CancellationToken ct = default)
    {
        return await clientApiResourceRepository.GetResourcesByClientIdAsync(clientId, ct);
    }

    /// <summary>
    /// 查询全部启用状态的资源�?
    /// </summary>
    public async Task<IReadOnlyList<ApiResource>> GetAllActiveResourcesAsync(
        CancellationToken ct = default)
    {
        return await apiResourceRepository.GetAllActiveAsync(ct);
    }

    private static ApiResourceDto MapDto(ApiResource resource)
    {
        return new ApiResourceDto(
            resource.Id,
            resource.Name,
            resource.DisplayName,
            resource.Audience,
            resource.Description,
            resource.IsActive,
            resource.CreatedAt);
    }

    private static int NormalizePageSize(int pageSize)
    {
        return pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
    }
}
