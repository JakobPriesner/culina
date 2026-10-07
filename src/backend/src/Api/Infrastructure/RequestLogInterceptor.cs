using Microsoft.AspNetCore.HttpLogging;

namespace Api.Infrastructure;

/// <summary>
/// Keeps the request line to API calls (assets, shell and probes get none) and adds the error code and route template.
/// The path goes through <see cref="SecretPaths"/>: a share token or invitation code would hand the log's reader what it opens.
/// </summary>
internal sealed class RequestLogInterceptor : IHttpLoggingInterceptor
{
    public ValueTask OnRequestAsync(HttpLoggingInterceptorContext logContext)
    {
        ArgumentNullException.ThrowIfNull(logContext);

        var path = logContext.HttpContext.Request.Path;

        if (!ApiPaths.IsApi(path))
        {
            logContext.LoggingFields = HttpLoggingFields.None;

            return ValueTask.CompletedTask;
        }

        // Under the framework's name for it, minus any share token or invitation code.
        logContext.Disable(HttpLoggingFields.RequestPath);
        logContext.AddParameter("Path", SecretPaths.Redact(path));

        return ValueTask.CompletedTask;
    }

    public ValueTask OnResponseAsync(HttpLoggingInterceptorContext logContext)
    {
        ArgumentNullException.ThrowIfNull(logContext);

        // A parameter added here would bring back the line the request side switched off.
        if (logContext.LoggingFields == HttpLoggingFields.None)
        {
            return ValueTask.CompletedTask;
        }

        var context = logContext.HttpContext;

        if (context.GetEndpoint() is RouteEndpoint endpoint)
        {
            logContext.AddParameter("Route", endpoint.RoutePattern.RawText);
        }

        if (RequestContext.ErrorCode(context) is { } code)
        {
            logContext.AddParameter("ErrorCode", code);
        }

        return ValueTask.CompletedTask;
    }
}
