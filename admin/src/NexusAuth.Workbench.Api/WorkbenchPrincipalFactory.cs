using System.Security.Claims;
using NexusAuth.Extension;

namespace NexusAuth.Workbench.Api;

internal static class WorkbenchPrincipalFactory
{
    public static ClaimsPrincipal Create(ValidatedIdToken idToken)
    {
        ArgumentNullException.ThrowIfNull(idToken);
        var name = idToken.Name ?? idToken.PreferredUsername ?? idToken.Subject;

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, idToken.Subject),
                new Claim(ClaimTypes.Name, name),
            ],
            WorkbenchAuthenticationDefaults.CookieScheme);

        return new ClaimsPrincipal(identity);
    }

    public static ClaimsPrincipal? CompactLegacyPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.FindFirst("access_token") is null
            && principal?.FindFirstValue(ClaimTypes.NameIdentifier)?.StartsWith("eyJ", StringComparison.Ordinal) != true)
        {
            return null;
        }

        return null;
    }
}
