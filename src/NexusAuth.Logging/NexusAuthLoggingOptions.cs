using Microsoft.Extensions.Configuration;
using Serilog.Events;

namespace NexusAuth.Logging;

/// <summary>
/// Shared file logging options used by the SSO and Workbench hosts.
/// </summary>
public sealed class NexusAuthLoggingOptions
{
    public const string SectionName = "NexusAuthLogging";

    public required string Module { get; init; }

    public required string DefaultFilePath { get; init; }

    public string FilePath { get; init; } = string.Empty;

    public LogEventLevel MinimumLevel { get; init; } = LogEventLevel.Information;

    public IReadOnlyDictionary<string, LogEventLevel> MinimumLevelOverrides { get; init; }
        = new Dictionary<string, LogEventLevel>(StringComparer.Ordinal);

    public long FileSizeLimitBytes { get; init; } = 100 * 1024 * 1024;

    public int RetainedFileCountLimit { get; init; } = 30;

    public bool RollOnFileSizeLimit { get; init; } = true;

    public bool Shared { get; init; } = true;

    public int FlushIntervalSeconds { get; init; } = 1;

    public string EffectiveFilePath => string.IsNullOrWhiteSpace(FilePath) ? DefaultFilePath : FilePath;

    public static NexusAuthLoggingOptions FromConfiguration(
        IConfiguration configuration,
        string module,
        string defaultFilePath)
    {
        var section = configuration.GetSection(SectionName);

        return new NexusAuthLoggingOptions
        {
            Module = module,
            DefaultFilePath = defaultFilePath,
            FilePath = section["FilePath"] ?? string.Empty,
            MinimumLevel = ParseLevel(section["MinimumLevel"]),
            MinimumLevelOverrides = ParseLevelOverrides(section.GetSection("MinimumLevelOverrides")),
            FileSizeLimitBytes = ParsePositiveLong(section["FileSizeLimitBytes"], 100 * 1024 * 1024),
            RetainedFileCountLimit = ParsePositiveInt(section["RetainedFileCountLimit"], 30),
            RollOnFileSizeLimit = ParseBool(section["RollOnFileSizeLimit"], true),
            Shared = ParseBool(section["Shared"], true),
            FlushIntervalSeconds = ParsePositiveInt(section["FlushIntervalSeconds"], 1),
        };
    }

    private static IReadOnlyDictionary<string, LogEventLevel> ParseLevelOverrides(
        IConfigurationSection section)
    {
        var overrides = new Dictionary<string, LogEventLevel>(StringComparer.Ordinal);
        foreach (var child in section.GetChildren())
        {
            if (Enum.TryParse<LogEventLevel>(child.Value, ignoreCase: true, out var level))
                overrides[child.Key] = level;
        }

        return overrides;
    }

    private static LogEventLevel ParseLevel(string? value)
    {
        return Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level)
            ? level
            : LogEventLevel.Information;
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    private static int ParsePositiveInt(string? value, int defaultValue)
    {
        return int.TryParse(value, out var result) && result > 0 ? result : defaultValue;
    }

    private static long ParsePositiveLong(string? value, long defaultValue)
    {
        return long.TryParse(value, out var result) && result > 0 ? result : defaultValue;
    }
}
