namespace NexusAuth.Application.Services.Tokens;

internal static class TokenSigningKeySourceUtilities
{
    public static string ResolvePath(string contentRootPath, string configuredPath, string settingName)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            throw new InvalidOperationException($"{settingName} path is not configured.");

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRootPath, configuredPath);
    }

    public static object CreateRsaJwk(string keyId, RSAParameters parameters)
    {
        return new Dictionary<string, object?>
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["kid"] = keyId,
            ["alg"] = "RS256",
            ["n"] = Base64UrlEncoder.Encode(parameters.Modulus),
            ["e"] = Base64UrlEncoder.Encode(parameters.Exponent),
        };
    }

    public static void RestrictFilePermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
