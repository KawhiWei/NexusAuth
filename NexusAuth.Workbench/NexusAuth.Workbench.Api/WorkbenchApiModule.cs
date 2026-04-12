using Luck.AppModule;
using Luck.AutoDependencyInjection;
using Luck.Framework.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
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

        services.AddNexusAuth(options =>
        {
            options.Authority = authority;
            options.ClientId = clientId;
            options.ClientSecret = clientSecret;
            options.RedirectUri = redirectUri;
            options.PostLogoutRedirectUri = postLogoutRedirectUri;
            options.Scope = scope;
        });

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = ".NexusAuth.Workbench";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.LoginPath = "/api/auth/login";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(24);
            });

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