using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Application;
using NexusAuth.Domain.AggregateRoots.ApiResources;

namespace NexusAuth.Application.Services.ApiResources;

public interface IApiResourceService : IScopedDependency
{
    Task<List<ApiResourceDto>> GetAllAsync(string? keyword = null, bool? isActive = null, CancellationToken ct = default);

    Task<PagedResult<ApiResourceDto>> GetPagedAsync(
        string? keyword = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default);

    Task<ApiResourceDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<ApiResource> RegisterAsync(
        string name,
        string displayName,
        string audience,
        string? description = null,
        CancellationToken ct = default);

    Task<ApiResourceDto> CreateAsync(CreateApiResourceRequest request, CancellationToken ct = default);

    Task<ApiResourceDto> UpdateAsync(Guid id, UpdateApiResourceRequest request, CancellationToken ct = default);

    Task<ApiResourceDto> UpdateStatusAsync(Guid id, bool isActive, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task AssignToClientAsync(
        Guid clientId,
        Guid apiResourceId,
        CancellationToken ct = default);

    Task RevokeFromClientAsync(
        Guid clientId,
        Guid apiResourceId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ApiResource>> GetClientResourcesAsync(
        Guid clientId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ApiResource>> GetAllActiveResourcesAsync(
        CancellationToken ct = default);
}

public record ApiResourceDto(
    Guid Id,
    string Name,
    string DisplayName,
    string Audience,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt);

public record CreateApiResourceRequest(
    string Name,
    string DisplayName,
    string Audience,
    string? Description);

public record UpdateApiResourceRequest(
    string? DisplayName,
    string? Audience,
    string? Description,
    bool? IsActive);

public record UpdateApiResourceStatusRequest(bool IsActive);
