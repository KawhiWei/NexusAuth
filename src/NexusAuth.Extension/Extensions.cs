using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http;

namespace NexusAuth.Extension;

public static class OidcWorkbenchExtensions
{
    public static IServiceCollection AddNexusAuth(
        this IServiceCollection services,
        Action<WorkbenchAuthOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<ValidatedWorkbenchAuthOptions>();

        services.AddHttpClient();
        services.AddSingleton<IFlowStateStore, InMemoryFlowStateStore>();
        services.AddScoped<IOidcWorkbenchService, OidcWorkbenchService>();

        return services;
    }
}

public class ValidatedWorkbenchAuthOptions
{
    public ValidatedWorkbenchAuthOptions(IOptions<WorkbenchAuthOptions> options)
    {
        var opts = options.Value;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(opts.Authority))
            errors.Add("Authority is required.");
        if (string.IsNullOrWhiteSpace(opts.ClientId))
            errors.Add("ClientId is required.");
        if (string.IsNullOrWhiteSpace(opts.RedirectUri))
            errors.Add("RedirectUri is required.");
        if (string.IsNullOrWhiteSpace(opts.PostLogoutRedirectUri))
            errors.Add("PostLogoutRedirectUri is required.");
        if (string.IsNullOrWhiteSpace(opts.Scope))
            errors.Add("Scope is required.");

        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));
    }
}