using Luck.Framework.Infrastructure.DependencyInjectionModule;
using NexusAuth.Domain.AggregateRoots.Users;

namespace NexusAuth.Application.Services.Security;

public interface ISecurityPolicyService : IScopedDependency
{
    PolicyCheckResult CheckClient(string clientId);

    PolicyCheckResult CheckUser(User user);
}

public record PolicyCheckResult(bool IsSuccess, string? Error)
{
    public static PolicyCheckResult Success() => new(true, null);

    public static PolicyCheckResult Failure(string error) => new(false, error);
}
