using Luck.AppModule;
using Luck.AutoDependencyInjection;
using Luck.Framework.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NexusAuth.Extension;
using NexusAuth.Persistence;

namespace NexusAuth.Workbench.Api;

[DependsOn(
    typeof(AutoDependencyAppModule),
    typeof(EntityFrameworkCoreModule)
)]
public class WorkbenchApiModule : LuckAppModule
{
    public override void ConfigureServices(ConfigureServicesContext context)
    {
        var services = context.Services;
        var configuration = services.GetConfiguration();

        var authority = configuration["Auth:Authority"];
        var clientId = configuration["Auth:ClientId"];
        var clientSecret = configuration["Auth:ClientSecret"];
        var redirectUri = configuration["Auth:RedirectUri"];
        var postLogoutRedirectUri = configuration["Auth:PostLogoutRedirectUri"];
        var scope = configuration["Auth:Scope"];
        var audience = configuration["Auth:Audience"];
        var requireHttpsMetadata = bool.TryParse(configuration["Auth:RequireHttpsMetadata"], out var parsedRequireHttpsMetadata)
            && parsedRequireHttpsMetadata;
        var requiredAuthConfiguration = RequireAuthConfiguration(
            authority,
            clientId,
            redirectUri,
            postLogoutRedirectUri,
            scope);

        services.AddNexusAuth(options =>
        {
            options.Authority = requiredAuthConfiguration.Authority;
            options.ClientId = requiredAuthConfiguration.ClientId;
            options.ClientSecret = clientSecret;
            options.RedirectUri = requiredAuthConfiguration.RedirectUri;
            options.PostLogoutRedirectUri = requiredAuthConfiguration.PostLogoutRedirectUri;
            options.Scope = requiredAuthConfiguration.Scope;
        });

        services.AddAuthentication(WorkbenchAuthenticationDefaults.Scheme)
            .AddPolicyScheme(WorkbenchAuthenticationDefaults.Scheme, "Cookie or Bearer", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var authorization = context.Request.Headers.Authorization.ToString();
                    if (!string.IsNullOrWhiteSpace(authorization)
                        && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return WorkbenchAuthenticationDefaults.BearerScheme;
                    }

                    return WorkbenchAuthenticationDefaults.CookieScheme;
                };
            })
            .AddCookie(WorkbenchAuthenticationDefaults.CookieScheme, options =>
            {
                options.Cookie.Name = ".NexusAuth.Workbench";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.LoginPath = "/api/auth/login";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(24);
            })
            .AddJwtBearer(WorkbenchAuthenticationDefaults.BearerScheme, options =>
            {
                var normalizedAuthority = requiredAuthConfiguration.Authority.TrimEnd('/');
                options.Authority = normalizedAuthority;
                options.RequireHttpsMetadata = requireHttpsMetadata;
                options.MetadataAddress = $"{normalizedAuthority}/.well-known/openid-configuration";
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = normalizedAuthority,
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    NameClaimType = "name",
                    RoleClaimType = "role",
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var tokenUse = context.Principal?.FindFirst("token_use")?.Value;
                        if (!string.Equals(tokenUse, "access_token", StringComparison.Ordinal))
                        {
                            context.Fail("Only access_token is accepted.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        base.ConfigureServices(context);
    }

    private static RequiredAuthConfiguration RequireAuthConfiguration(
        string? authority,
        string? clientId,
        string? redirectUri,
        string? postLogoutRedirectUri,
        string? scope)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(authority))
            errors.Add("Authority is required.");
        if (string.IsNullOrWhiteSpace(clientId))
            errors.Add("ClientId is required.");
        if (string.IsNullOrWhiteSpace(redirectUri))
            errors.Add("RedirectUri is required.");
        if (string.IsNullOrWhiteSpace(postLogoutRedirectUri))
            errors.Add("PostLogoutRedirectUri is required.");
        if (string.IsNullOrWhiteSpace(scope))
            errors.Add("Scope is required.");

        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));

        return new RequiredAuthConfiguration(
            authority ?? throw new InvalidOperationException("Authority is required."),
            clientId ?? throw new InvalidOperationException("ClientId is required."),
            redirectUri ?? throw new InvalidOperationException("RedirectUri is required."),
            postLogoutRedirectUri ?? throw new InvalidOperationException("PostLogoutRedirectUri is required."),
            scope ?? throw new InvalidOperationException("Scope is required."));
    }

    private sealed record RequiredAuthConfiguration(
        string Authority,
        string ClientId,
        string RedirectUri,
        string PostLogoutRedirectUri,
        string Scope);

    public override void ApplicationInitialization(ApplicationContext context)
    {
        var app = context.GetApplicationBuilder();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        base.ApplicationInitialization(context);
    }
}
