using IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Pipeline;

/// <summary>
/// Every route is signed-in only, or anonymous on purpose. There is no fallback policy (each endpoint opts in), so the real host's routes are
/// walked and every one that lets an anonymous request through must be named below.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class EndpointAuthorizationTests(PostgresFixture postgres)
{
    /// <summary>What anybody may call, signed in or not, and why; a route added here is a decision whose reason is reviewed.</summary>
    private static readonly HashSet<string> Anonymous =
    [
        // Probes: a load balancer has no account.
        "GET /health/live",
        "GET /health/ready",

        // The app itself, and the static files beside it.
        "ANY /{*path:nonfile}",
        "GET /.well-known/security.txt",

        // Setting up, before anybody has an account.
        "GET /api/v1/setup",

        // Becoming somebody: signing up, signing in, getting back in.
        "GET /api/v1/registration/policy",
        "POST /api/v1/users",
        "POST /api/v1/sessions",
        "POST /api/v1/password-resets",

        // A recipe somebody shared behind a link; the token is the key.
        "GET /api/v1/shared-recipes/{token}",
        "GET /api/v1/shared-recipes/{token}/image",
        "GET /api/v1/shared-recipes/{token}/nutrition",

        // The web app's own error reports: a sign-in page breaks too.
        "POST /api/v1/log-records"
    ];

    [Fact]
    public void EveryEndpoint_ShouldRequireASignedInCaller_UnlessItIsAnonymousOnPurpose()
    {
        var endpoints = postgres.Api.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        var open = endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => !RequiresAuthorization(endpoint))
            .Select(Name)
            .Where(name => !Anonymous.Contains(name))
            .Order(StringComparer.Ordinal);

        // Each one named here forgot RequireAuthorization(), or is anonymous on purpose and belongs in the list above.
        Assert.Empty(open);
    }

    [Fact]
    public void TheWalk_ShouldSeeTheRealHostsRoutes_SoTheRuleAboveIsNotVacuous()
    {
        var names = postgres.Api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(Name)
            .ToList();

        Assert.Contains("GET /api/v1/users/me", names);
        Assert.Contains("POST /api/v1/sessions", names);
        Assert.True(names.Count > 100, $"Only {names.Count} routes were found.");
    }

    /// <summary>Signed in only: an authorization requirement, with nothing letting an anonymous caller past it (<c>AllowAnonymous</c> wins).</summary>
    private static bool RequiresAuthorization(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
        && (endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0
            || endpoint.Metadata.GetMetadata<AuthorizationPolicy>() is not null);

    private static string Name(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
        var verb = methods is { Count: > 0 } ? string.Join(",", methods) : "ANY";

        return $"{verb} /{endpoint.RoutePattern.RawText?.TrimStart('/')}";
    }
}
