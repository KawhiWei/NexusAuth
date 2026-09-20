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
        var gatewayEnabled = configuration.IsWorkbenchGatewayEnabled();

        var authority = configuration["Auth:Authority"];
        var backchannelAuthority = configuration["Auth:BackchannelAuthority"];
        var clientId = configuration["Auth:ClientId"];
        var clientSecret = configuration["Auth:ClientSecret"];
        var redirectUri = configuration["Auth:RedirectUri"];
        var postLogoutRedirectUri = configuration["Auth:PostLogoutRedirectUri"];
        var scope = EnsureWorkbenchResourceScope(
            configuration["Auth:Scope"],
            configuration["Auth:Audience"]);
        var audience = configuration["Auth:Audience"];
        var signOutProvider = !bool.TryParse(configuration["Auth:SignOutProvider"], out var parsedSignOutProvider)
            || parsedSignOutProvider;
        var requireHttpsMetadata = bool.TryParse(configuration["Auth:RequireHttpsMetadata"], out var parsedRequireHttpsMetadata)
            && parsedRequireHttpsMetadata;
        var requiredAuthConfiguration = RequireAuthConfiguration(
            authority,
            clientId,
            clientSecret,
            redirectUri,
            postLogoutRedirectUri,
            scope,
            audience,
            gatewayEnabled);

        if (!gatewayEnabled)
        {
            services.AddNexusAuth(options =>
            {
                options.Authority = requiredAuthConfiguration.Authority;
                options.BackchannelAuthority = string.IsNullOrWhiteSpace(backchannelAuthority)
                    ? null
                    : backchannelAuthority;
                options.ClientId = requiredAuthConfiguration.ClientId;
                options.ClientSecret = requiredAuthConfiguration.ClientSecret;
                options.RedirectUri = requiredAuthConfiguration.RedirectUri;
                options.PostLogoutRedirectUri = requiredAuthConfiguration.PostLogoutRedirectUri;
                options.Scope = requiredAuthConfiguration.Scope;
                options.SignOutProvider = signOutProvider;
            });
            services.AddScoped<WorkbenchCookieAuthenticationEvents>();
        }

        var authentication = services.AddAuthentication(gatewayEnabled
            ? WorkbenchAuthenticationDefaults.BearerScheme
            : WorkbenchAuthenticationDefaults.Scheme);
        if (!gatewayEnabled)
        {
            authentication
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
                    options.EventsType = typeof(WorkbenchCookieAuthenticationEvents);
                });
        }
        authentication.AddJwtBearer(WorkbenchAuthenticationDefaults.BearerScheme, options =>
            {
                var normalizedAuthority = requiredAuthConfiguration.Authority.TrimEnd('/');
                var normalizedBackchannelAuthority = string.IsNullOrWhiteSpace(backchannelAuthority)
                    ? normalizedAuthority
                    : backchannelAuthority.TrimEnd('/');
                options.Authority = normalizedAuthority;
                options.RequireHttpsMetadata = requireHttpsMetadata;
                options.MetadataAddress = $"{normalizedBackchannelAuthority}/.well-known/openid-configuration";
                if (!string.Equals(normalizedAuthority, normalizedBackchannelAuthority, StringComparison.OrdinalIgnoreCase))
                {
                    options.Backchannel = new HttpClient(
                        new PublicAuthorityRewritingHandler(normalizedAuthority, normalizedBackchannelAuthority));
                }
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = normalizedAuthority,
                    ValidateAudience = true,
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
        services.Configure<WorkbenchBootstrapOptions>(configuration.GetSection(WorkbenchBootstrapOptions.SectionName));
        services.AddHostedService<WorkbenchClientCredentialHostedService>();

        base.ConfigureServices(context);
    }

    private static RequiredAuthConfiguration RequireAuthConfiguration(
        string? authority,
        string? clientId,
        string? clientSecret,
        string? redirectUri,
        string? postLogoutRedirectUri,
        string? scope,
        string? audience,
        bool gatewayEnabled)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(authority))
            errors.Add("Auth:Authority (NEXUSAUTH_WORKBENCH_AUTH_AUTHORITY) is required.");
        if (string.IsNullOrWhiteSpace(audience))
            errors.Add("Auth:Audience (NEXUSAUTH_WORKBENCH_AUTH_AUDIENCE) is required.");
        if (string.IsNullOrWhiteSpace(clientId))
            errors.Add("Auth:ClientId (NEXUSAUTH_WORKBENCH_AUTH_CLIENT_ID) is required for initialization.");
        if (string.IsNullOrWhiteSpace(clientSecret))
            errors.Add("Auth:ClientSecret (NEXUSAUTH_WORKBENCH_AUTH_CLIENT_SECRET) is required for initialization.");
        if (!gatewayEnabled && string.IsNullOrWhiteSpace(redirectUri))
            errors.Add("Auth:RedirectUri (NEXUSAUTH_WORKBENCH_AUTH_REDIRECT_URI) is required in non-gateway mode.");
        if (!gatewayEnabled && string.IsNullOrWhiteSpace(postLogoutRedirectUri))
            errors.Add("Auth:PostLogoutRedirectUri (NEXUSAUTH_WORKBENCH_AUTH_POST_LOGOUT_REDIRECT_URI) is required in non-gateway mode.");
        if (!gatewayEnabled && string.IsNullOrWhiteSpace(scope))
            errors.Add("Auth:Scope (NEXUSAUTH_WORKBENCH_AUTH_SCOPE) is required in non-gateway mode.");
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri)
            || (authorityUri.Scheme != Uri.UriSchemeHttp && authorityUri.Scheme != Uri.UriSchemeHttps))
            errors.Add("Auth:Authority (NEXUSAUTH_WORKBENCH_AUTH_AUTHORITY) must be an absolute HTTP(S) URI.");

        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));

        return new RequiredAuthConfiguration(
            authority ?? throw new InvalidOperationException("Authority is required."),
            clientId ?? throw new InvalidOperationException("ClientId is required."),
            clientSecret ?? throw new InvalidOperationException("ClientSecret is required."),
            redirectUri ?? string.Empty,
            postLogoutRedirectUri ?? string.Empty,
            scope ?? string.Empty);
    }

    private static string? EnsureWorkbenchResourceScope(string? scope, string? resourceName)
    {
        if (string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(resourceName))
            return scope;

        var scopes = scope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (!scopes.Contains(resourceName.Trim(), StringComparer.Ordinal))
            scopes.Add(resourceName.Trim());

        return string.Join(' ', scopes);
    }

    private sealed record RequiredAuthConfiguration(
        string Authority,
        string ClientId,
        string ClientSecret,
        string RedirectUri,
        string PostLogoutRedirectUri,
        string Scope);

    private sealed class PublicAuthorityRewritingHandler(string publicAuthority, string backchannelAuthority)
        : DelegatingHandler(new HttpClientHandler())
    {
        private readonly Uri _publicAuthority = new(publicAuthority, UriKind.Absolute);
        private readonly Uri _backchannelAuthority = new(backchannelAuthority, UriKind.Absolute);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri is { } requestUri
                && Uri.Compare(
                    requestUri,
                    _publicAuthority,
                    UriComponents.SchemeAndServer,
                    UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) == 0)
            {
                var builder = new UriBuilder(_backchannelAuthority)
                {
                    Path = requestUri.AbsolutePath,
                    Query = requestUri.Query,
                };
                request.RequestUri = builder.Uri;
            }

            return base.SendAsync(request, cancellationToken);
        }
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
