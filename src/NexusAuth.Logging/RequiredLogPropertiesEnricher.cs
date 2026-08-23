using Serilog.Core;
using Serilog.Events;

namespace NexusAuth.Logging;

/// <summary>
/// Guarantees that every event contains all fields required by the shared output template.
/// Existing non-empty values always take precedence over generated defaults.
/// </summary>
internal sealed class RequiredLogPropertiesEnricher(string module) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // A dash preserves the fixed template layout when the caller has no value for a dimension.
        AddIfMissing(logEvent, propertyFactory, NexusAuthLogPropertyNames.Module, module);
        AddIfMissing(logEvent, propertyFactory, NexusAuthLogPropertyNames.Category, "-");
        AddSubcategoryIfMissing(logEvent, propertyFactory);
        AddIfMissing(logEvent, propertyFactory, NexusAuthLogPropertyNames.TraceId, "-");
        AddIfMissing(logEvent, propertyFactory, NexusAuthLogPropertyNames.Filter1, "-");
        AddIfMissing(logEvent, propertyFactory, NexusAuthLogPropertyNames.Filter2, "-");
    }

    private static void AddSubcategoryIfMissing(
        LogEvent logEvent,
        ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Properties.TryGetValue(NexusAuthLogPropertyNames.Subcategory, out var existing)
            && !IsEmpty(existing))
        {
            return;
        }

        // ILogger<T> supplies SourceContext, so its final segment is a useful automatic subcategory.
        var subcategory = ExtractClassName(logEvent);
        logEvent.AddOrUpdateProperty(
            propertyFactory.CreateProperty(NexusAuthLogPropertyNames.Subcategory, subcategory));
    }

    private static string ExtractClassName(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue(NexusAuthLogPropertyNames.SourceContext, out var sourceContext)
            || sourceContext is not ScalarValue { Value: string source }
            || string.IsNullOrWhiteSpace(source))
        {
            return "-";
        }

        var className = source.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(className) ? "-" : className;
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
        // Treat empty structured values like an absent scalar so the output never contains blank slots.
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
