using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NexusAuth.Extension;

namespace NexusAuth.Workbench.Api;

/// <summary>
/// Keeps the Workbench authentication ticket alive while rotating its OIDC tokens.
/// </summary>
public sealed class WorkbenchCookieAuthenticationEvents(
    IOidcWorkbenchService oidcService) : CookieAuthenticationEvents
{
    private static readonly TimeSpan AccessTokenRefreshWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CompletedRefreshRetention = TimeSpan.FromSeconds(5);
    private static readonly ConcurrentDictionary<string, Lazy<Task<WorkbenchTokenResult>>> RefreshFlights = new(StringComparer.Ordinal);

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (context.Principal is null)
        {
            await RejectAndSignOutAsync(context);
            return;
        }

        // Keep old tickets readable after the ticket format changed. The token
        // properties below are present on all newly issued tickets.
        var compactPrincipal = WorkbenchPrincipalFactory.CompactLegacyPrincipal(context.Principal);
        if (compactPrincipal is not null)
        {
            context.ReplacePrincipal(compactPrincipal);
            context.ShouldRenew = true;
        }

        var refreshToken = context.Properties.GetTokenValue("refresh_token");
        var expiresAtValue = context.Properties.GetTokenValue("expires_at");
        if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(expiresAtValue))
            return;

        if (!DateTimeOffset.TryParse(
                expiresAtValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var expiresAt))
        {
            await RejectAndSignOutAsync(context);
            return;
        }

        if (expiresAt > DateTimeOffset.UtcNow.Add(AccessTokenRefreshWindow))
            return;

        try
        {
            var refreshed = await RefreshWithSingleFlightAsync(
                refreshToken,
                context.HttpContext.RequestAborted);
            var idToken = refreshed.IdToken ?? context.Properties.GetTokenValue("id_token");
            if (string.IsNullOrWhiteSpace(refreshed.AccessToken)
                || string.IsNullOrWhiteSpace(refreshed.RefreshToken)
                || string.IsNullOrWhiteSpace(idToken))
            {
                throw new InvalidOperationException("The refresh response did not contain the required tokens.");
            }

            var refreshedExpiresAt = DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn);
            var tokens = context.Properties.GetTokens()
                .Where(token => token.Name is not ("access_token" or "refresh_token" or "id_token" or "expires_at"))
                .Concat(
                [
                    new AuthenticationToken { Name = "access_token", Value = refreshed.AccessToken },
                    new AuthenticationToken { Name = "refresh_token", Value = refreshed.RefreshToken },
                    new AuthenticationToken { Name = "id_token", Value = idToken },
                    new AuthenticationToken { Name = "expires_at", Value = refreshedExpiresAt.ToString("O", CultureInfo.InvariantCulture) },
                ]);
            context.Properties.StoreTokens(tokens);

            // Renew the independent Workbench session window together with the
            // ticket so both cookie sliding and token sliding remain active.
            var now = DateTimeOffset.UtcNow;
            context.Properties.IssuedUtc = now;
            context.Properties.ExpiresUtc = now.AddHours(24);
            context.Properties.AllowRefresh = true;
            context.ShouldRenew = true;
        }
        catch
        {
            await RejectAndSignOutAsync(context);
        }
    }

    private async Task<WorkbenchTokenResult> RefreshWithSingleFlightAsync(
        string refreshToken,
        CancellationToken requestCancellation)
    {
        var refresh = RefreshFlights.GetOrAdd(
            refreshToken,
            token => new Lazy<Task<WorkbenchTokenResult>>(
                () => RefreshAndRetainAsync(token),
                LazyThreadSafetyMode.ExecutionAndPublication));
        var task = refresh.Value;

        // The underlying rotation request is intentionally independent from a
        // single request's disconnect. Other concurrent requests can still use
        // the one result and receive the same rotated refresh token.
        return await task.WaitAsync(requestCancellation);
    }

    private async Task<WorkbenchTokenResult> RefreshAndRetainAsync(string refreshToken)
    {
        try
        {
            return await oidcService.RefreshTokensAsync(refreshToken, CancellationToken.None);
        }
        finally
        {
            _ = RemoveRefreshFlightLaterAsync(refreshToken);
        }
    }

    private static async Task RemoveRefreshFlightLaterAsync(string refreshToken)
    {
        await Task.Delay(CompletedRefreshRetention);
        RefreshFlights.TryRemove(refreshToken, out _);
    }

    private static async Task RejectAndSignOutAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(WorkbenchAuthenticationDefaults.CookieScheme);
    }
}
