namespace NexusAuth.Extension;

public interface IOidcWorkbenchService
{
    string Authority { get; }
    string ClientId { get; }
    string RedirectUri { get; }
    string PostLogoutRedirectUri { get; }
    string Scope { get; }
    bool SignOutProvider { get; }
    IFlowStateStore FlowStateStore { get; }
    Task<DiscoveryDocument> FetchDiscoveryAsync(CancellationToken ct);
    string GenerateCodeVerifier();
    (string codeChallenge, string codeVerifier) GeneratePkce();
    Task<WorkbenchTokenResult> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct);
    Task<WorkbenchTokenResult> RefreshTokensAsync(string refreshToken, CancellationToken ct);
}

public sealed record WorkbenchTokenResult(
    string AccessToken,
    string RefreshToken,
    string? IdToken,
    int ExpiresIn);
