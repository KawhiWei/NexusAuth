namespace NexusAuth.Logging;

/// <summary>
/// Contains the canonical Serilog templates used by every NexusAuth host.
/// </summary>
public static class NexusAuthLogTemplates
{
    /// <summary>
    /// Renders each event as a single line with fixed-position operational fields for scanning and parsing.
    /// </summary>
    public const string Output =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}][{Level:u3}][{Module}][{Category}][{Subcategory}][{RequestTraceId}][{Filter1}][{Filter2}][{Message:lj}{Exception}]\n";

    /// <summary>The message emitted by Serilog when an HTTP request completes.</summary>
    public const string HttpRequestCompleted =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
}
