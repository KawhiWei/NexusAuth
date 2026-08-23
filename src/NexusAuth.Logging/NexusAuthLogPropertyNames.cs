namespace NexusAuth.Logging;

/// <summary>
/// Defines the structured property names shared by application logs and output templates.
/// Keeping these names centralized prevents log producers and sinks from using different keys.
/// </summary>
public static class NexusAuthLogPropertyNames
{
    /// <summary>The host that emitted the event, such as NexusAuth.SSO or NexusAuth.Workbench.</summary>
    public const string Module = "Module";

    /// <summary>The business area responsible for the event, such as Token or Authentication.</summary>
    public const string Category = "Category";

    /// <summary>The operation, class, or HTTP path that further identifies the event source.</summary>
    public const string Subcategory = "Subcategory";

    /// <summary>The distributed trace identifier used to correlate logs across a request.</summary>
    public const string TraceId = "RequestTraceId";

    /// <summary>The first searchable business dimension, for example a client ID or HTTP method.</summary>
    public const string Filter1 = "Filter1";

    /// <summary>The second searchable business dimension, for example a result or status code.</summary>
    public const string Filter2 = "Filter2";

    /// <summary>The Serilog source context, normally the fully qualified class name.</summary>
    public const string SourceContext = "SourceContext";
}
