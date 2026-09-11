using Api.Infrastructure;
using Domain.Shared;

namespace Api.Middleware;

/// <summary>
/// Rejects query parameters an endpoint does not declare.
/// </summary>
/// <remarks>
/// <para>
/// A silently ignored filter returns wrong data that looks right, which is far
/// worse than an error: the caller believes it asked for vegan recipes under
/// 30 minutes and gets everything. A typo in a parameter name must fail loudly.
/// </para>
/// <para>
/// Endpoints declare their parameters with
/// <c>.WithQueryParameters("query", "tag")</c>. An endpoint that declares none
/// accepts none.
/// </para>
/// </remarks>
/// <param name="next">The rest of the pipeline.</param>
internal sealed class QueryParameterGuardMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, ILogger<QueryParameterGuardMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        // Only API paths are guarded: the SPA's own routes carry whatever query
        // the browser and the app put there.
        if (!ApiPaths.IsApi(context.Request.Path) || context.Request.Query.Count == 0)
        {
            return next(context);
        }

        var endpoint = context.GetEndpoint();

        if (endpoint is null)
        {
            // Nothing matched this path. Letting routing answer with its own
            // 404 is more useful than blaming a query parameter on an address
            // that does not exist.
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
        logger.Rejected(context.Request.Method, context.Request.Path, error.Code);

        return CustomResults.WriteProblemAsync(context, error);
    }
}
