using System.Text.Json;

namespace NexusAuth.Application.Services;

public sealed class OidcRequestedClaims
{
    public static readonly OidcRequestedClaims Empty = new([], []);

    public HashSet<string> IdTokenClaims { get; }

    public HashSet<string> UserInfoClaims { get; }

    private OidcRequestedClaims(HashSet<string> idTokenClaims, HashSet<string> userInfoClaims)
    {
        IdTokenClaims = idTokenClaims;
        UserInfoClaims = userInfoClaims;
    }

    /// <summary>
    /// 创建请求 claims 模型。
    /// </summary>
    public static OidcRequestedClaims Create(IEnumerable<string>? idTokenClaims = null, IEnumerable<string>? userInfoClaims = null)
    {
        return new OidcRequestedClaims(
            idTokenClaims?.ToHashSet(StringComparer.Ordinal) ?? [],
            userInfoClaims?.ToHashSet(StringComparer.Ordinal) ?? []);
    }

    // 中文注释：OIDC claims 参数是 JSON 对象，这里只提取被请求的 claim 名称。
    /// <summary>
    /// 解析 OIDC claims JSON 参数。
    /// </summary>
    public static OidcRequestedClaims Parse(string? claimsJson)
    {
        if (string.IsNullOrWhiteSpace(claimsJson))
            return Empty;

        using var document = JsonDocument.Parse(claimsJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("OIDC claims parameter must be a JSON object.");

        return new OidcRequestedClaims(
            ParseClaimSection(document.RootElement, "id_token"),
            ParseClaimSection(document.RootElement, "userinfo"));
    }

    /// <summary>
    /// 判断是否请求了某个 id_token claim。
    /// </summary>
    public bool RequestsIdTokenClaim(string claimName)
    {
        return IdTokenClaims.Contains(claimName);
    }

    /// <summary>
    /// 判断是否请求了某个 userinfo claim。
    /// </summary>
    public bool RequestsUserInfoClaim(string claimName)
    {
        return UserInfoClaims.Contains(claimName);
    }

    private static HashSet<string> ParseClaimSection(JsonElement root, string sectionName)
    {
        var claims = new HashSet<string>(StringComparer.Ordinal);
        if (!root.TryGetProperty(sectionName, out var section) || section.ValueKind != JsonValueKind.Object)
            return claims;

        foreach (var property in section.EnumerateObject())
        {
            claims.Add(property.Name);
        }

        return claims;
    }
}
