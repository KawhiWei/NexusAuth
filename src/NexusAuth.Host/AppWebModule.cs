using Luck.AppModule;
using Luck.AutoDependencyInjection;
using Luck.Framework.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
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

    /// <summary>
    /// 注册 Host 层所需的配置与基础认证服务。
    /// </summary>
    public override void ConfigureServices(ConfigureServicesContext context)
    {
        var services = context.Services;
        var configuration = services.GetConfiguration();

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<NexusAuthSecurityOptions>(configuration.GetSection("Security"));

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

    /// <summary>
    /// 配置应用初始化中间件管道。
    /// </summary>
    public override void ApplicationInitialization(ApplicationContext context)
    {
        var app = context.GetApplicationBuilder();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        base.ApplicationInitialization(context);
    }
}
