using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.AggregateRoots.ApiResources;

namespace NexusAuth.Application.Services.ApiResources;

public interface IApiResourceService : IScopedDependency
{
    Task<ApiResource> RegisterAsync(
        string name,
        string displayName,
        string audience,
        string? description = null,
        CancellationToken ct = default);

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
