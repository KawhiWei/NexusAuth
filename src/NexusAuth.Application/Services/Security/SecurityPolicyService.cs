
namespace NexusAuth.Application.Services.Security;

public class SecurityPolicyService(IOptions<NexusAuthSecurityOptions> options) : ISecurityPolicyService
{
    private readonly NexusAuthSecurityOptions options = options.Value;

    /// <summary>
    /// 执行客户端黑白名单策略校验�?
    /// </summary>
    public PolicyCheckResult CheckClient(string clientId)
    {
        if (Contains(options.ClientBlacklist, clientId))
            return PolicyCheckResult.Failure("Client is blocked by security policy.");

        if (options.ClientWhitelist.Count > 0 && !Contains(options.ClientWhitelist, clientId))
            return PolicyCheckResult.Failure("Client is not in the allowed list.");

        return PolicyCheckResult.Success();
    }

    /// <summary>
    /// 执行用户黑白名单策略校验�?
    /// </summary>
    public PolicyCheckResult CheckUser(User user)
    {
        if (Contains(options.UserBlacklist, user.Username) || Contains(options.UserBlacklist, user.Id.ToString()))
            return PolicyCheckResult.Failure("User is blocked by security policy.");

        if (options.UserWhitelist.Count > 0
            && !Contains(options.UserWhitelist, user.Username)
            && !Contains(options.UserWhitelist, user.Id.ToString()))
            return PolicyCheckResult.Failure("User is not in the allowed list.");

        return PolicyCheckResult.Success();
    }

    private static bool Contains(IEnumerable<string> source, string value)
    {
        return source.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
    }
}
