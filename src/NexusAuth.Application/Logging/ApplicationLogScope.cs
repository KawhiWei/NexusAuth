namespace NexusAuth.Application.Logging;

/// <summary>
/// Applies the common application log fields without coupling the application
/// layer to a concrete logging provider.
/// </summary>
public static class ApplicationLogScope
{
    /// <summary>
    /// Adds a business category and two stable filter values to the current log scope.
    /// The subcategory is intentionally left to the shared logger enricher, which derives
    /// it from the logger's declaring class unless a request event supplies an explicit value.
    /// </summary>
    public static IDisposable Begin(
        ILogger logger,
        string category,
        string? filter1 = null,
        string? filter2 = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return logger.BeginScope(new Dictionary<string, object?>
        {
            ["Category"] = Normalize(category),
            ["Filter1"] = Normalize(filter1),
            ["Filter2"] = Normalize(filter2),
        }) ?? NoopScope.Instance;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "-";

        var normalized = value.Trim()
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        return normalized.Length <= 256 ? normalized : normalized[..256];
    }

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();

        public void Dispose()
        {
        }
    }
}
