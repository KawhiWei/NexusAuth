namespace NexusAuth.Application.Logging;

/// <summary>
/// Applies the common application log fields without coupling the application
/// layer to a concrete logging provider.
/// </summary>
public static class ApplicationLogScope
{
    /// <summary>
    /// Adds a business category, identifier, and outcome to the current log scope.
    /// The subcategory is intentionally left to the shared logger enricher, which derives
    /// it from the logger's declaring class.
    /// </summary>
    public static IDisposable Begin(
        ILogger logger,
        string category,
        string? businessId = null,
        string? outcome = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var properties = new Dictionary<string, object?>
        {
            ["Category"] = Normalize(category),
        };

        // Filter1 belongs to the request/message entry scope and must remain stable.
        AddIfPresent(properties, "Filter2", businessId);
        AddIfPresent(properties, "Outcome", outcome);

        return logger.BeginScope(properties) ?? NoopScope.Instance;
    }

    private static void AddIfPresent(
        IDictionary<string, object?> properties,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            properties[name] = Normalize(value);
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
