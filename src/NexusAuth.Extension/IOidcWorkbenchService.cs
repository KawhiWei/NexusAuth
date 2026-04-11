namespace NexusAuth.Extension;

public interface IOidcWorkbenchService
{
    string Authority { get; }
    string ClientId { get; }
    string RedirectUri { get; }
    string PostLogoutRedirectUri { get; }
    string Scope { get; }
    IFlowStateStore FlowStateStore { get; }
    Task<DiscoveryDocument> FetchDiscoveryAsync(CancellationToken ct);
    string GenerateCodeVerifier();
    (string codeChallenge, string codeVerifier) GeneratePkce();
    Task<(string accessToken, string idToken, int expiresIn)> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct);
}