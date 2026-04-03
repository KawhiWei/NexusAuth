using NexusAuth.Domain.AggregateRoots.ApiResources;
using NexusAuth.Domain.Entities;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Application.Services;

public class ApiResourceService : IApiResourceService
{
    private readonly IApiResourceRepository _apiResourceRepository;
    private readonly IClientApiResourceRepository _clientApiResourceRepository;

    public ApiResourceService(
        IApiResourceRepository apiResourceRepository,
        IClientApiResourceRepository clientApiResourceRepository)
    {
        _apiResourceRepository = apiResourceRepository;
        _clientApiResourceRepository = clientApiResourceRepository;
    }

    public async Task<ApiResource> RegisterAsync(
        string name,
        string displayName,
        string audience,
        string? description = null,
        CancellationToken ct = default)
    {
        var existing = await _apiResourceRepository.FindByNameAsync(name, ct);
        if (existing is not null)
            throw new InvalidOperationException($"API resource with name '{name}' already exists.");

        var resource = ApiResource.Create(name, displayName, audience, description);
        await _apiResourceRepository.AddAsync(resource, ct);

        return resource;
    }

    public async Task AssignToClientAsync(
        Guid clientId,
        Guid apiResourceId,
        CancellationToken ct = default)
    {
        var association = ClientApiResource.Create(clientId, apiResourceId);
        await _clientApiResourceRepository.AddAsync(association, ct);
    }

    public async Task RevokeFromClientAsync(
        Guid clientId,
        Guid apiResourceId,
        CancellationToken ct = default)
    {
        await _clientApiResourceRepository.RemoveAsync(clientId, apiResourceId, ct);
    }

    public async Task<IReadOnlyList<ApiResource>> GetClientResourcesAsync(
        Guid clientId,
        CancellationToken ct = default)
    {
        return await _clientApiResourceRepository.GetResourcesByClientIdAsync(clientId, ct);
    }

    public async Task<IReadOnlyList<ApiResource>> GetAllActiveResourcesAsync(
        CancellationToken ct = default)
    {
        return await _apiResourceRepository.GetAllActiveAsync(ct);
    }
}
