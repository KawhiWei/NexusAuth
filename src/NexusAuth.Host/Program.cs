using Luck.AutoDependencyInjection;
using Luck.Logging.Serilog;
using NexusAuth.Host;
using NexusAuth.Shared.Logging;

var builder = WebApplication.CreateBuilder(args);
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
    app.UseNexusAuthRequestLogContext();
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
