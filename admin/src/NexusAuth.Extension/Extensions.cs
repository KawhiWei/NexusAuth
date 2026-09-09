using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NexusAuth.Extension;

/// <summary>
/// 中文：提供将 NexusAuth OIDC Workbench 功能注册到依赖注入容器的扩展方法。
/// English: Provides extension methods for registering NexusAuth OIDC Workbench services with dependency injection.
/// </summary>
public static class OidcWorkbenchExtensions
{
    /// <summary>
    /// 中文：注册 NexusAuth OIDC 配置、HTTP 客户端、流程状态存储和 Workbench 服务。
    /// English: Registers NexusAuth OIDC options, HTTP clients, flow-state storage, and Workbench services.
    /// </summary>
    /// <param name="services">中文：要添加服务的依赖注入集合。English: The dependency injection collection to which services are added.</param>
    /// <param name="configure">中文：用于配置 Workbench OIDC 客户端的委托。English: A delegate that configures the Workbench OIDC client.</param>
    /// <returns>中文：同一个服务集合，以支持链式调用。English: The same service collection for call chaining.</returns>
    /// <exception cref="ArgumentNullException">中文：<paramref name="services"/> 或 <paramref name="configure"/> 为 <see langword="null"/>。English: <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
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

/// <summary>
/// 中文：在应用启动解析时验证必需的 Workbench OIDC 配置。
/// English: Validates required Workbench OIDC settings when this service is resolved during application startup.
/// </summary>
public class ValidatedWorkbenchAuthOptions
{
    /// <summary>
    /// 中文：创建验证器并检查当前 Workbench OIDC 配置。
    /// English: Creates the validator and checks the current Workbench OIDC configuration.
    /// </summary>
    /// <param name="options">中文：由配置系统提供的 Workbench OIDC 选项。English: The Workbench OIDC options supplied by the configuration system.</param>
    /// <exception cref="InvalidOperationException">中文：一个或多个必需配置项为空。English: One or more required settings are empty.</exception>
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
