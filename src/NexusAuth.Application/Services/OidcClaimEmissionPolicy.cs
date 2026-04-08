namespace NexusAuth.Application.Services;

public static class OidcClaimEmissionPolicy
{
    /// <summary>
    /// 判断某个 claim 是否应该按当前 OIDC claims 请求返回给客户端。
    /// 主要调用方：userinfo 端点与 id_token 组装逻辑。
    /// </summary>
    public static bool ShouldEmitRequestedClaim(OidcRequestedClaims requestedClaims, string claimName, string? actualValue)
    {
        if (!requestedClaims.RequestsClaim(claimName))
            return false;

        var claimRequest = requestedClaims.GetClaimRequest(claimName);
        return claimRequest?.Matches(actualValue) ?? true;
    }
}
