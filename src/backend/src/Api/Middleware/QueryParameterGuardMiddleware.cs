using Api.Infrastructure;
using Application.Telemetry;
using Domain.Shared;

namespace Api.Middleware;

/// <summary>
/// Rejects query parameters an endpoint does not declare (via <c>.WithQueryParameters("query", "tag")</c>),
/// because a silently ignored filter returns wrong data that looks right.
/// </summary>
/// <param name="next">The rest of the pipeline.</param>
internal sealed class QueryParameterGuardMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, ILogger<QueryParameterGuardMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        // Only API paths are guarded; the SPA's routes carry arbitrary queries.
        if (!ApiPaths.IsApi(context.Request.Path) || context.Request.Query.Count == 0)
        {
            return next(context);
        }

        // Only an endpoint mapped for a method has parameters to check. Nothing matched, the SPA
        // fallback (an unknown API path), the framework's 405 endpoint (a wrong method) and the
        // setup host's catch-all carry none: they answer 404, 405 or 503 themselves, which beats
        // blaming a parameter on an address or method that does not exist.
        if (context.GetEndpoint()?.Metadata is not { } metadata
            || metadata.GetMetadata<IHttpMethodMetadata>() is null)
        {
            return next(context);
        }

        var allowed = metadata.GetMetadata<AllowedQueryParameters>();

        foreach (var (name, values) in context.Request.Query)
        {
            if (allowed?.Contains(name) != true)
            {
                return Reject(context, logger, RequestErrors.UnknownQueryParameter(name));
            }

            if (values.Count > 1 && !allowed.IsRepeatable(name))
            {
                return Reject(context, logger, RequestErrors.RepeatedQueryParameter(name));
            }
        }

        return next(context);
    }

    private static Task Reject(HttpContext context, ILogger logger, Error error)
    {
        logger.Rejected(context.Request, error.Code);
        CulinaTelemetry.QueryRejections.Add(1);

        return CustomResults.WriteProblemAsync(context, error);
    }
}
