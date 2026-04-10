namespace NexusAuth.Application.Services.ApiResources;

public class ApiResourceService(
    IApiResourceRepository apiResourceRepository,
    IClientApiResourceRepository clientApiResourceRepository) : IApiResourceService
{

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
}
