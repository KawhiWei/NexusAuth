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
    public const string OutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}][{Module}][{SourceContext}][{Subcategory}][{RequestTraceId}][{Filter1}][{Filter2}][{Level:u3} {Message:lj}{Exception}]\n";

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
            using (LogContext.PushProperty("RequestTraceId", GetTraceId(context)))
            using (LogContext.PushProperty("Subcategory", GetPath(context)))
            using (LogContext.PushProperty("Filter1", context.Request.Method))
            {
                await next();
            }
        });

        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.GetLevel = static (context, _, exception) =>
                exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError
                    ? LogEventLevel.Error
                    : context.Response.StatusCode >= StatusCodes.Status400BadRequest
                        ? LogEventLevel.Warning
                        : LogEventLevel.Information;
            options.EnrichDiagnosticContext = static (diagnosticContext, context) =>
            {
                diagnosticContext.Set("RequestTraceId", GetTraceId(context));
                diagnosticContext.Set("Subcategory", GetPath(context));
                diagnosticContext.Set("Filter1", context.Request.Method);
                diagnosticContext.Set(
                    "Filter2",
                    context.Response.StatusCode.ToString(CultureInfo.InvariantCulture));
            };
        });

        return app;
    }

    public static void LogStartupFailure(Exception exception)
    {
        Log.Fatal(exception, "Host terminated unexpectedly during startup or execution.");
    }

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
            .WriteTo.Console(outputTemplate: OutputTemplate)
            .WriteTo.File(
                filePath,
                outputTemplate: OutputTemplate,
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

    private sealed class RequiredLogPropertiesEnricher(string module) : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            AddIfMissing(logEvent, propertyFactory, "Module", module);
            AddIfMissing(logEvent, propertyFactory, "SourceContext", "-");
            AddIfMissing(logEvent, propertyFactory, "Subcategory", "-");
            AddIfMissing(logEvent, propertyFactory, "RequestTraceId", "-");
            AddIfMissing(logEvent, propertyFactory, "Filter1", "-");
            AddIfMissing(logEvent, propertyFactory, "Filter2", "-");
        }

        private static void AddIfMissing(
            LogEvent logEvent,
            ILogEventPropertyFactory propertyFactory,
            string name,
            object value)
        {
            if (!logEvent.Properties.TryGetValue(name, out var existing)
                || IsEmpty(existing))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(name, value));
            }
        }

        private static bool IsEmpty(LogEventPropertyValue value)
        {
            return value switch
            {
                ScalarValue { Value: null } => true,
                ScalarValue { Value: string text } => string.IsNullOrWhiteSpace(text),
                SequenceValue { Elements.Count: 0 } => true,
                DictionaryValue { Elements.Count: 0 } => true,
                StructureValue { Properties.Count: 0 } => true,
                _ => false,
            };
        }
    }
}
