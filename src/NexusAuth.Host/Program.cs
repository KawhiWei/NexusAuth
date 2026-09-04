using Luck.AutoDependencyInjection;
using Luck.Logging.Serilog;
using Microsoft.Extensions.Configuration;
using NexusAuth.Host;

var builder = WebApplication.CreateBuilder(args);
AddSingleUnderscoreEnvironmentVariables(builder.Configuration);
builder.AddLuckSerilog();

try
{
    builder.Services.AddControllers();
    builder.Services.AddRazorPages();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddApplication<AppWebModule>();

    var app = builder.Build();

    app.UseStaticFiles();
    app.InitializeApplication();
    app.UseLuckRequestLogContext();
    app.MapControllers();
    app.MapRazorPages();

    app.Run();
}
catch (Exception exception)
{
    LoggingExtensions.LogStartupFailure(exception);
    throw;
}
finally
{
    LoggingExtensions.CloseAndFlush();
}

static void AddSingleUnderscoreEnvironmentVariables(ConfigurationManager configuration)
{
    var keyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["NEXUSAUTH_CONNECTION_STRINGS_DEFAULT"] = "ConnectionStrings:Default",
        ["NEXUSAUTH_JWT_ISSUER"] = "Jwt:Issuer",
        ["NEXUSAUTH_JWT_SIGNING_MODE"] = "Jwt:SigningMode",
        ["NEXUSAUTH_JWT_DEVELOPMENT_SIGNING_CERTIFICATE_PATH"] = "Jwt:DevelopmentSigningCertificatePath",
        ["NEXUSAUTH_JWT_DEVELOPMENT_SIGNING_CERTIFICATE_PASSWORD"] = "Jwt:DevelopmentSigningCertificatePassword",
        ["NEXUSAUTH_JWT_SIGNING_CERTIFICATE_PATH"] = "Jwt:SigningCertificatePath",
        ["NEXUSAUTH_JWT_SIGNING_CERTIFICATE_PASSWORD"] = "Jwt:SigningCertificatePassword",
        ["NEXUSAUTH_JWT_DEVELOPMENT_SIGNING_KEY_PATH"] = "Jwt:DevelopmentSigningKeyPath",
        ["NEXUSAUTH_JWT_SIGNING_KEY_PATH"] = "Jwt:SigningKeyPath",
        ["NEXUSAUTH_JWT_ACCESS_TOKEN_LIFETIME_MINUTES"] = "Jwt:AccessTokenLifetimeMinutes",
        ["NEXUSAUTH_JWT_REFRESH_TOKEN_LIFETIME_MINUTES"] = "Jwt:RefreshTokenLifetimeMinutes",
        ["NEXUSAUTH_SLIDER_CAPTCHA_ENABLED"] = "SliderCaptcha:Enabled",
        ["NEXUSAUTH_SLIDER_CAPTCHA_CHALLENGE_LIFETIME_SECONDS"] = "SliderCaptcha:ChallengeLifetimeSeconds",
        ["NEXUSAUTH_SLIDER_CAPTCHA_TOLERANCE_PIXELS"] = "SliderCaptcha:TolerancePixels",
        ["NEXUSAUTH_SLIDER_CAPTCHA_TRACK_WIDTH_PIXELS"] = "SliderCaptcha:TrackWidthPixels",
        ["NEXUSAUTH_LOGIN_FLOW_REMEMBER_ME_LIFETIME_DAYS"] = "LoginFlow:RememberMeLifetimeDays",
        ["NEXUSAUTH_LOGIN_PAGE_BRAND_NAME"] = "LoginPage:BrandName",
        ["NEXUSAUTH_LOGIN_PAGE_BRAND_LOGO_URL"] = "LoginPage:BrandLogoUrl",
        ["NEXUSAUTH_LOGIN_PAGE_MARKETING_HEADING"] = "LoginPage:MarketingHeading",
        ["NEXUSAUTH_LOGIN_PAGE_MARKETING_DESCRIPTION"] = "LoginPage:MarketingDescription",
        ["NEXUSAUTH_LOGIN_PAGE_LOGIN_TITLE"] = "LoginPage:LoginTitle",
        ["NEXUSAUTH_LOGIN_PAGE_LOGIN_SUBTITLE"] = "LoginPage:LoginSubtitle",
        ["NEXUSAUTH_SELF_REGISTRATION_ENABLED"] = "SelfRegistration:Enabled",
        ["NEXUSAUTH_BOOTSTRAP_ADMIN_USERNAME"] = "BootstrapAdmin:Username",
        ["NEXUSAUTH_BOOTSTRAP_ADMIN_PASSWORD"] = "BootstrapAdmin:Password",
        ["NEXUSAUTH_BOOTSTRAP_ADMIN_NICKNAME"] = "BootstrapAdmin:Nickname",
        ["NEXUSAUTH_BOOTSTRAP_ADMIN_EMAIL"] = "BootstrapAdmin:Email",
        ["NEXUSAUTH_LUCK_LOGGING_MODULE"] = "LuckLogging:Module",
        ["NEXUSAUTH_LUCK_LOGGING_FILE_PATH"] = "LuckLogging:FilePath",
        ["NEXUSAUTH_LUCK_LOGGING_MINIMUM_LEVEL"] = "LuckLogging:MinimumLevel",
        ["NEXUSAUTH_LUCK_LOGGING_FILE_SIZE_LIMIT_BYTES"] = "LuckLogging:FileSizeLimitBytes",
        ["NEXUSAUTH_LUCK_LOGGING_RETAINED_FILE_COUNT_LIMIT"] = "LuckLogging:RetainedFileCountLimit",
        ["NEXUSAUTH_LUCK_LOGGING_ROLL_ON_FILE_SIZE_LIMIT"] = "LuckLogging:RollOnFileSizeLimit",
        ["NEXUSAUTH_LUCK_LOGGING_SHARED"] = "LuckLogging:Shared",
        ["NEXUSAUTH_LUCK_LOGGING_FLUSH_INTERVAL_SECONDS"] = "LuckLogging:FlushIntervalSeconds",
        ["NEXUSAUTH_LUCK_LOGGING_MINIMUM_LEVEL_OVERRIDES_MICROSOFT_ASPNETCORE"] = "LuckLogging:MinimumLevelOverrides:Microsoft.AspNetCore",
        ["NEXUSAUTH_LUCK_LOGGING_MINIMUM_LEVEL_OVERRIDES_MICROSOFT_ENTITYFRAMEWORKCORE_DATABASE_COMMAND"] = "LuckLogging:MinimumLevelOverrides:Microsoft.EntityFrameworkCore.Database.Command"
    };
    var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    foreach (var (environmentName, configurationKey) in keyMap)
    {
        var value = Environment.GetEnvironmentVariable(environmentName);
        if (value is null)
            continue;

        values[configurationKey] = value;
    }

    if (values.Count > 0)
        configuration.AddInMemoryCollection(values);
}
