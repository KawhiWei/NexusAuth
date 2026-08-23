using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Core;
using Serilog.Context;
using Serilog.Events;

namespace NexusAuth.Logging;

/// <summary>
/// Shared Serilog bootstrap, sinks and HTTP context enrichment for NexusAuth hosts.
/// </summary>
public static class NexusAuthLoggingExtensions
{
    /// <summary>
    /// Configures the bootstrap and host Serilog loggers with the shared NexusAuth sinks and enrichers.
    /// </summary>
    /// <param name="builder">The web application builder to configure.</param>
    /// <param name="module">The module name written to every event from this host.</param>
    /// <param name="defaultFilePath">The log file path used when configuration does not override it.</param>
    /// <returns>The same builder instance for fluent startup configuration.</returns>
    public static WebApplicationBuilder UseNexusAuthSerilog(
        this WebApplicationBuilder builder,
        string module,
        string defaultFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultFilePath);

        var bootstrapOptions = NexusAuthLoggingOptions.FromConfiguration(
            builder.Configuration,
            module,
            defaultFilePath);

        // Install a logger before the host is built so configuration and startup failures are captured.
        Log.Logger = CreateLogger(bootstrapOptions, builder.Environment.ContentRootPath);

        builder.Host.UseSerilog((context, _, loggerConfiguration) =>
        {
            var options = NexusAuthLoggingOptions.FromConfiguration(
                context.Configuration,
                module,
                defaultFilePath);

            ConfigureLogger(loggerConfiguration, options, context.HostingEnvironment.ContentRootPath);
        });

        return builder;
    }

    /// <summary>
    /// Adds request logging and pushes request fields into every log emitted while the request runs.
    /// This should be the first middleware added by each host.
    /// </summary>
    public static IApplicationBuilder UseNexusAuthRequestLogging(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            using (LogContext.PushProperty(NexusAuthLogPropertyNames.TraceId, GetTraceId(context)))
            using (LogContext.PushProperty(NexusAuthLogPropertyNames.Filter1, context.Request.Method))
            {
                await next();
            }
        });

        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = NexusAuthLogTemplates.HttpRequestCompleted;
            options.GetLevel = static (context, _, exception) =>
                exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError
                    ? LogEventLevel.Error
                    : context.Response.StatusCode >= StatusCodes.Status400BadRequest
                        ? LogEventLevel.Warning
                        : LogEventLevel.Information;
            options.EnrichDiagnosticContext = static (diagnosticContext, context) =>
            {
                diagnosticContext.Set(NexusAuthLogPropertyNames.TraceId, GetTraceId(context));
                diagnosticContext.Set(NexusAuthLogPropertyNames.Category, "HTTP");
                diagnosticContext.Set(NexusAuthLogPropertyNames.Subcategory, GetPath(context));
                diagnosticContext.Set(NexusAuthLogPropertyNames.Filter1, context.Request.Method);
                diagnosticContext.Set(
                    NexusAuthLogPropertyNames.Filter2,
                    context.Response.StatusCode.ToString(CultureInfo.InvariantCulture));
            };
        });

        return app;
    }

    /// <summary>Records an unrecoverable exception raised while the host starts or runs.</summary>
    /// <param name="exception">The exception that caused the host to terminate.</param>
    public static void LogStartupFailure(Exception exception)
    {
        Log.Fatal(exception, "Host terminated unexpectedly during startup or execution.");
    }

    /// <summary>Flushes buffered events and releases Serilog sinks during host shutdown.</summary>
    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }

    private static Logger CreateLogger(
        NexusAuthLoggingOptions options,
        string contentRootPath)
    {
        var loggerConfiguration = new LoggerConfiguration();
        ConfigureLogger(loggerConfiguration, options, contentRootPath);
        return loggerConfiguration.CreateLogger();
    }

    private static void ConfigureLogger(
        LoggerConfiguration loggerConfiguration,
        NexusAuthLoggingOptions options,
        string contentRootPath)
    {
        var filePath = ResolveFilePath(options.EffectiveFilePath, contentRootPath);

        loggerConfiguration
            .MinimumLevel.Is(options.MinimumLevel)
            .Enrich.FromLogContext()
            .Enrich.With(new RequiredLogPropertiesEnricher(options.Module))
            .WriteTo.Console(outputTemplate: NexusAuthLogTemplates.Output)
            .WriteTo.File(
                filePath,
                outputTemplate: NexusAuthLogTemplates.Output,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: options.RollOnFileSizeLimit,
                fileSizeLimitBytes: options.FileSizeLimitBytes,
                retainedFileCountLimit: options.RetainedFileCountLimit,
                shared: options.Shared,
                flushToDiskInterval: TimeSpan.FromSeconds(options.FlushIntervalSeconds));

        foreach (var (source, level) in options.MinimumLevelOverrides)
            loggerConfiguration.MinimumLevel.Override(source, level);
    }

    private static string ResolveFilePath(string filePath, string contentRootPath)
    {
        var resolvedPath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(contentRootPath, filePath);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return resolvedPath;
    }

    private static string GetTraceId(HttpContext context)
    {
        var activityTraceId = Activity.Current?.TraceId.ToString();
        return string.IsNullOrWhiteSpace(activityTraceId) || activityTraceId.All(static character => character == '0')
            ? context.TraceIdentifier
            : activityTraceId;
    }

    private static string GetPath(HttpContext context)
    {
        return context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
    }
}
