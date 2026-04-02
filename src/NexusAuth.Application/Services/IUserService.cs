using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.AggregateRoots.Users;

namespace NexusAuth.Application.Services;

public interface IUserService : IScopedDependency
{
    Task<Guid> RegisterAsync(
        string username,
        string rawPassword,
        string nickname,
        string? email = null,
        string? phoneNumber = null,
        Gender gender = Gender.Unknown,
        string? ethnicity = null,
        CancellationToken ct = default);

    Task<User?> ValidateCredentialsAsync(
        string identifier,
        string rawPassword,
        CancellationToken ct = default);

    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);
}
