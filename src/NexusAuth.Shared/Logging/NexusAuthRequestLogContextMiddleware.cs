using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace NexusAuth.Shared.Logging;

/// <summary>
/// Creates the structured request context and writes one completion log per request.
/// </summary>
public sealed class NexusAuthRequestLogContextMiddleware
{
    private const string SuccessOutcome = "Success";
    private const string FailureOutcome = "Failure";
    private const string HttpCategory = "HTTP";
    private const string PageCategory = "Page";

    private readonly RequestDelegate _next;
    private readonly ILogger<NexusAuthRequestLogContextMiddleware> _logger;

    public NexusAuthRequestLogContextMiddleware(
        RequestDelegate next,
        ILogger<NexusAuthRequestLogContextMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);

        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();
        var requestTraceId = ResolveRequestTraceId(context);
        var filter1 = Guid.NewGuid().ToString("N");
        var filter2 = ResolveFilter2(context);
        var (category, subcategory) = ResolveEndpointContext(context);
        var method = NexusAuthLogContext.Sanitize(context.Request.Method)
            ?? NexusAuthLogContext.MissingFilterValue;
        var path = NexusAuthLogContext.Sanitize(context.Request.Path.Value)
            ?? "/";

        using var requestScope = _logger.BeginScope(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [NexusAuthLogContext.RequestTraceIdPropertyName] = requestTraceId,
                [NexusAuthLogContext.Filter1PropertyName] = filter1,
                [NexusAuthLogContext.Filter2PropertyName] = filter2,
                [NexusAuthLogContext.CategoryPropertyName] = category,
                [NexusAuthLogContext.SubcategoryPropertyName] = subcategory,
            });

        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            LogCompletion(
                method,
                path,
                stopwatch.ElapsedMilliseconds,
                FailureOutcome,
                ResolveFailureStatusCode(context),
                exception);
            throw;
        }

        stopwatch.Stop();
        var statusCode = context.Response.StatusCode;
        LogCompletion(
            method,
            path,
            stopwatch.ElapsedMilliseconds,
            statusCode < StatusCodes.Status400BadRequest ? SuccessOutcome : FailureOutcome,
            statusCode,
            exception: null);
    }

    private void LogCompletion(
        string method,
        string path,
        long elapsedMilliseconds,
        string outcome,
        int statusCode,
        Exception? exception)
    {
        using var completionScope = _logger.BeginScope(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [NexusAuthLogContext.OutcomePropertyName] = outcome,
                [NexusAuthLogContext.StatusCodePropertyName] = statusCode,
                [NexusAuthLogContext.ElapsedMsPropertyName] = elapsedMilliseconds,
                [NexusAuthLogContext.MethodPropertyName] = method,
                [NexusAuthLogContext.PathPropertyName] = path,
            });

        const string message =
            "Request completed. Outcome={Outcome} StatusCode={StatusCode} "
            + "ElapsedMs={ElapsedMs} Method={Method} Path={Path}";

        if (exception is not null)
        {
            _logger.LogError(
                exception,
                message,
                outcome,
                statusCode,
                elapsedMilliseconds,
                method,
                path);
            return;
        }

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(message, outcome, statusCode, elapsedMilliseconds, method, path);
        }
        else if (statusCode >= StatusCodes.Status400BadRequest)
        {
            _logger.LogWarning(message, outcome, statusCode, elapsedMilliseconds, method, path);
        }
        else
        {
            _logger.LogInformation(message, outcome, statusCode, elapsedMilliseconds, method, path);
        }
    }

    private static string ResolveRequestTraceId(HttpContext context)
    {
        var activity = Activity.Current;
        if (activity is not null && activity.TraceId != default)
        {
            return activity.TraceId.ToString();
        }

        return NexusAuthLogContext.Sanitize(context.TraceIdentifier)
            ?? NexusAuthLogContext.MissingFilterValue;
    }

    private static string ResolveFilter2(HttpContext context)
    {
        var user = context.User;
        var userId = NexusAuthLogContext.Sanitize(
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        if (userId is not null)
        {
            return userId;
        }

        return NexusAuthLogContext.Sanitize(user.FindFirst("sub")?.Value)
            ?? NexusAuthLogContext.MissingFilterValue;
    }

    private static (string Category, string Subcategory) ResolveEndpointContext(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var controllerAction = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (controllerAction is not null)
        {
            return (
                NexusAuthLogContext.Sanitize(controllerAction.ControllerName)
                    ?? NexusAuthLogContext.MissingFilterValue,
                NexusAuthLogContext.Sanitize(controllerAction.ActionName)
                    ?? NexusAuthLogContext.MissingFilterValue);
        }

        var pageAction = endpoint?.Metadata.GetMetadata<PageActionDescriptor>();
        if (pageAction is not null)
        {
            return (
                PageCategory,
                NexusAuthLogContext.Sanitize(pageAction.ViewEnginePath)
                    ?? NexusAuthLogContext.MissingFilterValue);
        }

        return (
            HttpCategory,
            NexusAuthLogContext.Sanitize(endpoint?.DisplayName)
                ?? NexusAuthLogContext.Sanitize(context.Request.Method)
                ?? NexusAuthLogContext.MissingFilterValue);
    }

    private static int ResolveFailureStatusCode(HttpContext context)
    {
        return context.Response.StatusCode >= StatusCodes.Status400BadRequest
            ? context.Response.StatusCode
            : StatusCodes.Status500InternalServerError;
    }
}
