using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NexusAuth.Workbench.Api;

internal static class WorkbenchPrincipalFactory
{
    public static ClaimsPrincipal Create(string idToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(idToken);
        var subject = jwt.Claims.FirstOrDefault(claim => claim.Type == "sub")?.Value
            ?? throw new InvalidOperationException("The ID token does not contain a subject.");
        var name = jwt.Claims.FirstOrDefault(claim => claim.Type == "name")?.Value
            ?? jwt.Claims.FirstOrDefault(claim => claim.Type == "preferred_username")?.Value
            ?? subject;

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, subject),
                new Claim(ClaimTypes.Name, name),
                new Claim("id_token", idToken),
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

        var idToken = principal.FindFirstValue("id_token");
        return string.IsNullOrWhiteSpace(idToken) ? null : Create(idToken);
    }
}
