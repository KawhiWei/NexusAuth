using Luck.AutoDependencyInjection;
using NexusAuth.Host;
using NexusAuth.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.UseNexusAuthSerilog("NexusAuth.SSO", "logs/nexusauth-sso-.log");

try
{
    builder.Services.AddControllers();
    builder.Services.AddRazorPages();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddApplication<AppWebModule>();

    var app = builder.Build();

    app.UseNexusAuthRequestLogging();
    app.UseStaticFiles();
    app.MapControllers();
    app.MapRazorPages();
    app.InitializeApplication();

    app.Run();
}
catch (Exception exception)
{
    NexusAuthLoggingExtensions.LogStartupFailure(exception);
    throw;
}
finally
{
    NexusAuthLoggingExtensions.CloseAndFlush();
}
