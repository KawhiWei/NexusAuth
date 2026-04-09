using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

internal static class ClientAssertionHelper
{
    public static async Task AppendPrivateKeyJwtAsync(
        Dictionary<string, string> form,
        string clientId,
        string tokenEndpoint,
        string privateKeyPath,
        string keyId,
        CancellationToken ct = default)
    {
        var resolvedPath = ResolvePrivateKeyPath(privateKeyPath);
        var pem = await File.ReadAllTextAsync(resolvedPath, ct);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        var keyParameters = rsa.ExportParameters(true);

        var now = DateTimeOffset.UtcNow;
        var credentials = new SigningCredentials(new RsaSecurityKey(keyParameters) { KeyId = keyId }, SecurityAlgorithms.RsaSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = clientId,
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, clientId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            Audience = tokenEndpoint,
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = now.AddMinutes(5).UtcDateTime,
            SigningCredentials = credentials,
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        form["client_id"] = clientId;
        form["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
        form["client_assertion"] = handler.WriteToken(token);
    }

    private static string ResolvePrivateKeyPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath) && File.Exists(configuredPath))
            return configuredPath;

        var fileName = Path.GetFileName(configuredPath);

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, configuredPath),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", configuredPath),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "keys", fileName),
            Path.Combine(Environment.CurrentDirectory, configuredPath),
            Path.Combine(Environment.CurrentDirectory, "demo", "src", "Demo.ClientCredentials", "keys", fileName),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
                return fullPath;
        }

        throw new FileNotFoundException($"Private key file was not found: {configuredPath}");
    }
}
