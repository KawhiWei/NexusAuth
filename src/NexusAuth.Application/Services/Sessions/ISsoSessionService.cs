using Luck.Framework.Infrastructure.DependencyInjectionModule;

namespace NexusAuth.Application.Services.Sessions;

public interface ISsoSessionService : IScopedDependency
{
    Task<Guid> CreateAsync(Guid userId, CancellationToken ct = default);

    Task<bool> IsActiveAsync(Guid sessionId, Guid userId, CancellationToken ct = default);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}
