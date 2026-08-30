using Luck.AppModule;
using Luck.AutoDependencyInjection;
using Luck.Framework.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using NexusAuth.Application.Services;
using NexusAuth.Persistence;
using System.Threading.RateLimiting;

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

        services.AddNexusAuthTokenSigning(configuration);
        services.Configure<NexusAuthSecurityOptions>(configuration.GetSection("Security"));
        services.Configure<BootstrapAdminOptions>(configuration.GetSection("BootstrapAdmin"));
        services.AddHostedService<BootstrapAdminHostedService>();
        services.AddScoped<SsoCookieAuthenticationEvents>();

        // Keep abuse controls at the host boundary so every sensitive
        // endpoint, including Razor login/device pages, shares one policy.
        // The limiter is partitioned by endpoint family and source address;
        // account lockout is handled separately in the application layer.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(CreateRateLimitPartition);
            options.OnRejected = static async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                response.Headers.RetryAfter = "60";
                response.ContentType = "application/json";

                await response.WriteAsJsonAsync(
                    new
                    {
                        error = "too_many_requests",
                        error_description = "Too many requests. Please retry later."
                    },
                    cancellationToken);
            };
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
                options.EventsType = typeof(SsoCookieAuthenticationEvents);
            });
        services.AddAuthorization();

        base.ConfigureServices(context);
    }

    /// <summary>
    /// 配置应用初始化中间件管道。
    /// </summary>
    public override void ApplicationInitialization(ApplicationContext context)
    {
        var app = context.GetApplicationBuilder();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        base.ApplicationInitialization(context);
    }

    private static RateLimitPartition<string> CreateRateLimitPartition(HttpContext context)
    {
        var path = context.Request.Path;
        var (family, tokenLimit) = GetRateLimitFamily(path);
        if (family is null)
            return RateLimitPartition.GetNoLimiter("unlimited");

        var address = context.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(address))
            address = "unknown";

        var partitionKey = $"{family}:{address}";
        return RateLimitPartition.GetTokenBucketLimiter(
            partitionKey,
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = tokenLimit,
                TokensPerPeriod = tokenLimit,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                AutoReplenishment = true,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            });
    }

    private static (string? Family, int TokenLimit) GetRateLimitFamily(PathString path)
    {
        if (path.StartsWithSegments("/account/login"))
            return ("login", 10);

        if (path.StartsWithSegments("/connect/token"))
            return ("token", 60);

        if (path.StartsWithSegments("/connect/deviceauthorization"))
            return ("device_authorization", 20);

        if (path.StartsWithSegments("/connect/introspect"))
            return ("introspection", 120);

        if (path.StartsWithSegments("/connect/revocation"))
            return ("revocation", 60);

        if (path.StartsWithSegments("/device"))
            return ("device_verification", 30);

        return (null, 0);
    }
}
