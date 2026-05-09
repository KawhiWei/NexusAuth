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

        services.AddNexusAuth(options =>
        {
            options.Authority = authority;
            options.ClientId = clientId;
            options.ClientSecret = clientSecret;
            options.RedirectUri = redirectUri;
            options.PostLogoutRedirectUri = postLogoutRedirectUri;
            options.Scope = scope;
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
                var normalizedAuthority = authority!.TrimEnd('/');
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

    public override void ApplicationInitialization(ApplicationContext context)
    {
        var app = context.GetApplicationBuilder();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        base.ApplicationInitialization(context);
    }
}
