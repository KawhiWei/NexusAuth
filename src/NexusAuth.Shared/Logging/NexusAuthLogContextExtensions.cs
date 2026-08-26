using Microsoft.AspNetCore.Builder;

namespace NexusAuth.Shared.Logging;

public static class NexusAuthLogContextExtensions
{
    /// <summary>
    /// Adds the structured request log scope to the ASP.NET Core pipeline.
    /// </summary>
    public static IApplicationBuilder UseNexusAuthRequestLogContext(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<NexusAuthRequestLogContextMiddleware>();
    }
}
