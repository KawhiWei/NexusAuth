namespace NexusAuth.Shared.Logging;

/// <summary>
/// Structured property names used by the request log context.
/// </summary>
public static class NexusAuthLogContext
{
    public const string RequestTraceIdPropertyName = "RequestTraceId";
    public const string Filter1PropertyName = "Filter1";
    public const string Filter2PropertyName = "Filter2";
    public const string CategoryPropertyName = "Category";
    public const string SubcategoryPropertyName = "Subcategory";
    public const string OutcomePropertyName = "Outcome";
    public const string StatusCodePropertyName = "StatusCode";
    public const string ElapsedMsPropertyName = "ElapsedMs";
    public const string MethodPropertyName = "Method";
    public const string PathPropertyName = "Path";

    public const string MissingFilterValue = "-";

    internal const int MaxValueLength = 256;

    internal static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim()
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (normalized.Length == 0)
            return null;

        return normalized.Length <= MaxValueLength
            ? normalized
            : normalized[..MaxValueLength];
    }
}
