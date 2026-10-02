using Microsoft.AspNetCore.HttpLogging;

namespace Api.Infrastructure;

/// <summary>
/// Keeps the request line to API calls and adds what the framework cannot see.
/// </summary>
/// <remarks>
/// <para>
/// Static assets, the app shell and health probes are most of the requests and
/// none of the questions, so they get no line.
/// </para>
/// <para>
/// An expected failure — a recipe that is not there, a wrong password — is a
/// value, not an exception, and leaves nothing else in the log. Its code on
/// the request line is what lets the log alone say why a request failed. The
/// route template says which endpoint answered without reading ids out of a
/// path.
/// </para>
/// </remarks>
internal sealed class RequestLogInterceptor : IHttpLoggingInterceptor
{
    public ValueTask OnRequestAsync(HttpLoggingInterceptorContext logContext)
    {
        ArgumentNullException.ThrowIfNull(logContext);

        if (!ApiPaths.IsApi(logContext.HttpContext.Request.Path))
        {
            logContext.LoggingFields = HttpLoggingFields.None;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OnResponseAsync(HttpLoggingInterceptorContext logContext)
    {
        ArgumentNullException.ThrowIfNull(logContext);

        // A parameter added here would bring back the line the request side
        // switched off.
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
