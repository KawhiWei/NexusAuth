using Microsoft.Extensions.Options;
using NexusAuth.Domain.AggregateRoots.Users;

namespace NexusAuth.Application.Services;

public class SecurityPolicyService : ISecurityPolicyService
{
    private readonly NexusAuthSecurityOptions _options;

    public SecurityPolicyService(IOptions<NexusAuthSecurityOptions> options)
    {
        _options = options.Value;
    }

    public PolicyCheckResult CheckClient(string clientId)
    {
        if (Contains(_options.ClientBlacklist, clientId))
            return PolicyCheckResult.Failure("Client is blocked by security policy.");

        if (_options.ClientWhitelist.Count > 0 && !Contains(_options.ClientWhitelist, clientId))
            return PolicyCheckResult.Failure("Client is not in the allowed list.");

        return PolicyCheckResult.Success();
    }

    public PolicyCheckResult CheckUser(User user)
    {
        if (Contains(_options.UserBlacklist, user.Username) || Contains(_options.UserBlacklist, user.Id.ToString()))
            return PolicyCheckResult.Failure("User is blocked by security policy.");

        if (_options.UserWhitelist.Count > 0
            && !Contains(_options.UserWhitelist, user.Username)
            && !Contains(_options.UserWhitelist, user.Id.ToString()))
            return PolicyCheckResult.Failure("User is not in the allowed list.");

        return PolicyCheckResult.Success();
    }

    private static bool Contains(IEnumerable<string> source, string value)
    {
        return source.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
    }
}
