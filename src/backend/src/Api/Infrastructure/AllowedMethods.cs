using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;

namespace Api.Infrastructure;

/// <summary>The methods the API answers at a path, for the <c>405</c> routing cannot give.</summary>
/// <remarks>
/// The app-shell fallback accepts every method, so routing always has a candidate and never builds
/// its own 405 endpoint: a wrong method lands in the fallback like an unknown path. This tells the
/// two apart by matching the path, constraints included, against every endpoint mapped for a method.
/// </remarks>
/// <param name="sources">Where the endpoints are, read on first use once mapping is done.</param>
/// <param name="policies">Builds the route constraints, the way routing does.</param>
internal sealed class AllowedMethods(
    IEnumerable<EndpointDataSource> sources,
    ParameterPolicyFactory policies)
{
    private IReadOnlyList<Route>? routes;

    /// <summary>Every method some endpoint takes at this request's path; empty when none matches.</summary>
    public IReadOnlyList<string> At(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        routes ??= Read();

        return
        [
            .. routes
                .Where(route => route.Matches(context, policies))
                .SelectMany(route => route.Methods)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.Ordinal)
        ];
    }

    private List<Route> Read() =>
    [
        .. sources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => (endpoint.RoutePattern, Methods: endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()))
            .Where(endpoint => endpoint.Methods is { HttpMethods.Count: > 0 })
            .Select(endpoint => new Route(endpoint.RoutePattern, endpoint.Methods!.HttpMethods))
    ];

    private sealed class Route(RoutePattern pattern, IReadOnlyList<string> methods)
    {
        private readonly TemplateMatcher matcher = new(new RouteTemplate(pattern), new RouteValueDictionary(pattern.Defaults));

        public IReadOnlyList<string> Methods => methods;

        public bool Matches(HttpContext context, ParameterPolicyFactory policies)
        {
            var values = new RouteValueDictionary();

            if (!matcher.TryMatch(context.Request.Path, values))
            {
                return false;
            }

            // /recipes/{recipeId:guid} is not at /recipes/abc, which stays a 404.
            foreach (var (name, references) in pattern.ParameterPolicies)
            {
                var parameter = pattern.GetParameter(name);

                foreach (var reference in references)
                {
                    if (policies.Create(parameter, reference) is IRouteConstraint constraint
                        && !constraint.Match(context, route: null, name, values, RouteDirection.IncomingRequest))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
