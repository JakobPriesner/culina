using Api.Infrastructure;
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

        var endpoint = context.GetEndpoint();

        if (endpoint is null)
        {
            // Nothing matched; let routing answer with its own 404.
            return next(context);
        }

        var allowed = endpoint.Metadata.GetMetadata<AllowedQueryParameters>();

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

        return CustomResults.WriteProblemAsync(context, error);
    }
}
