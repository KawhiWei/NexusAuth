using Luck.AppModule;
using Luck.AutoDependencyInjection;
using Luck.Framework.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using NexusAuth.Application.Services;
using NexusAuth.Persistence;

namespace NexusAuth.Host;

[DependsOn(
    typeof(AutoDependencyAppModule),
    typeof(EntityFrameworkCoreModule)
)]
public class AppWebModule : LuckAppModule
{
    public const string AuthenticationScheme = "NexusAuth.Identity";

    public override void ConfigureServices(ConfigureServicesContext context)
    {
        var services = context.Services;
        var configuration = services.GetConfiguration();

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));

        // CORS: allow the demo app (and other configured origins) to call NexusAuth APIs
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        // Cookie Authentication for SSO login session
        services.AddAuthentication(AuthenticationScheme)
            .AddCookie(AuthenticationScheme, options =>
            {
                options.Cookie.Name = ".NexusAuth.Identity";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.LoginPath = "/account/login";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.Events = new CookieAuthenticationEvents
                {
                    OnSigningIn = context =>
                    {
                        // Enforce absolute expiration of 24 hours
                        context.Properties.IssuedUtc = DateTimeOffset.UtcNow;
                        context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24);
                        return Task.CompletedTask;
                    }
                };
            });

        base.ConfigureServices(context);
    }

    public override void ApplicationInitialization(ApplicationContext context)
    {
        var app = context.GetApplicationBuilder();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();

        base.ApplicationInitialization(context);
    }
}
