using Microsoft.Extensions.Configuration;

namespace NexusAuth.Workbench.Api;

public static class WorkbenchConfigurationExtensions
{
    private static readonly IReadOnlyDictionary<string, string> SingleUnderscoreEnvironmentVariables =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NEXUSAUTH_WORKBENCH_CONNECTION_STRINGS_DEFAULT"] = "ConnectionStrings:Default",
            ["NEXUSAUTH_WORKBENCH_AUTH_AUTHORITY"] = "Auth:Authority",
            ["NEXUSAUTH_WORKBENCH_AUTH_BACKCHANNEL_AUTHORITY"] = "Auth:BackchannelAuthority",
            ["NEXUSAUTH_WORKBENCH_AUTH_CLIENT_ID"] = "Auth:ClientId",
            ["NEXUSAUTH_WORKBENCH_AUTH_CLIENT_SECRET"] = "Auth:ClientSecret",
            ["NEXUSAUTH_WORKBENCH_AUTH_REDIRECT_URI"] = "Auth:RedirectUri",
            ["NEXUSAUTH_WORKBENCH_AUTH_POST_LOGOUT_REDIRECT_URI"] = "Auth:PostLogoutRedirectUri",
            ["NEXUSAUTH_WORKBENCH_AUTH_SCOPE"] = "Auth:Scope",
            ["NEXUSAUTH_WORKBENCH_AUTH_AUDIENCE"] = "Auth:Audience",
            ["NEXUSAUTH_WORKBENCH_AUTH_REQUIRE_HTTPS_METADATA"] = "Auth:RequireHttpsMetadata",
            ["NEXUSAUTH_WORKBENCH_AUTH_SIGN_OUT_PROVIDER"] = "Auth:SignOutProvider",
            ["NEXUSAUTH_WORKBENCH_BOOTSTRAP_RESOURCE_NAME"] = "Bootstrap:ResourceName",
            ["NEXUSAUTH_WORKBENCH_BOOTSTRAP_RESOURCE_DISPLAY_NAME"] = "Bootstrap:ResourceDisplayName",
            ["NEXUSAUTH_WORKBENCH_BOOTSTRAP_RESOURCE_DESCRIPTION"] = "Bootstrap:ResourceDescription",
            ["NEXUSAUTH_WORKBENCH_BOOTSTRAP_ALLOWED_SCOPES"] = "Bootstrap:AllowedScopes",
            ["NEXUSAUTH_WORKBENCH_BOOTSTRAP_CLIENT_NAME"] = "Bootstrap:ClientName",
            ["NEXUSAUTH_WORKBENCH_BOOTSTRAP_CLIENT_DESCRIPTION"] = "Bootstrap:ClientDescription",
            ["NEXUSAUTH_LUCK_LOGGING_MODULE"] = "LuckLogging:Module",
            ["NEXUSAUTH_LUCK_LOGGING_FILE_PATH"] = "LuckLogging:FilePath",
            ["NEXUSAUTH_LUCK_LOGGING_MINIMUM_LEVEL"] = "LuckLogging:MinimumLevel",
            ["NEXUSAUTH_LUCK_LOGGING_FILE_SIZE_LIMIT_BYTES"] = "LuckLogging:FileSizeLimitBytes",
            ["NEXUSAUTH_LUCK_LOGGING_RETAINED_FILE_COUNT_LIMIT"] = "LuckLogging:RetainedFileCountLimit",
            ["NEXUSAUTH_LUCK_LOGGING_ROLL_ON_FILE_SIZE_LIMIT"] = "LuckLogging:RollOnFileSizeLimit",
            ["NEXUSAUTH_LUCK_LOGGING_SHARED"] = "LuckLogging:Shared",
            ["NEXUSAUTH_LUCK_LOGGING_FLUSH_INTERVAL_SECONDS"] = "LuckLogging:FlushIntervalSeconds",
            ["NEXUSAUTH_LUCK_LOGGING_MINIMUM_LEVEL_OVERRIDES_MICROSOFT_ASPNETCORE"] = "LuckLogging:MinimumLevelOverrides:Microsoft.AspNetCore",
            ["NEXUSAUTH_LUCK_LOGGING_MINIMUM_LEVEL_OVERRIDES_MICROSOFT_ENTITYFRAMEWORKCORE_DATABASE_COMMAND"] = "LuckLogging:MinimumLevelOverrides:Microsoft.EntityFrameworkCore.Database.Command",
        };

    public static ConfigurationManager AddWorkbenchEnvironmentVariables(this ConfigurationManager configuration)
    {
        var values = SingleUnderscoreEnvironmentVariables
            .Select(item => (ConfigurationKey: item.Value, Value: Environment.GetEnvironmentVariable(item.Key)))
            .Where(item => item.Value is not null)
            .ToDictionary(item => item.ConfigurationKey, item => item.Value, StringComparer.OrdinalIgnoreCase);

        if (values.Count > 0)
            configuration.AddInMemoryCollection(values);

        return configuration;
    }
}
