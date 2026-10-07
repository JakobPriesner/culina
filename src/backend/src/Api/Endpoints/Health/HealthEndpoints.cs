using Application.Abstractions;

namespace Api.Endpoints.Health;

/// <summary>Liveness and readiness.</summary>
/// <remarks>
/// Neither lives under <c>/api</c>, requires authentication, or is traced: they are most of the
/// traffic and none of the information.
/// </remarks>
internal static class HealthEndpoints
{
    internal static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Liveness answers "is the process running" and touches nothing else: a dependency failure
        // would make an orchestrator restart a process that works.
        app.MapGet("/health/live", () => Results.Ok(new HealthResponse("live")))
            .WithName("healthLive")
            .ExcludeFromDescription()
            .AllowAnonymous();

        // Readiness answers "can it serve requests", which for Culina means the
        // database answers.
        app.MapGet("/health/ready", async (IDatabaseProbe database, CancellationToken cancellationToken) =>
            {
                var reachable = await database.IsReachableAsync(cancellationToken).ConfigureAwait(false);

                return reachable
                    ? Results.Ok(new HealthResponse("ready"))
                    : Results.Json(
                        new HealthResponse("unavailable"),
                        statusCode: StatusCodes.Status503ServiceUnavailable);
            })
            .WithName("healthReady")
            .ExcludeFromDescription()
            .AllowAnonymous();

        return app;
    }

    /// <summary>Health for the host that runs before there is a database.</summary>
    /// <remarks>
    /// Ready, deliberately: the setup screen it serves is servable, and a proxy routing only to
    /// healthy containers (Traefik) would otherwise hide the page that makes it healthy.
    /// </remarks>
    internal static WebApplication MapSetupHealthEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/health/live", () => Results.Ok(new HealthResponse("live")))
            .WithName("healthLive")
            .ExcludeFromDescription()
            .AllowAnonymous();

        app.MapGet("/health/ready", () => Results.Ok(new HealthResponse("setup")))
            .WithName("healthReady")
            .ExcludeFromDescription()
            .AllowAnonymous();

        return app;
    }

    private sealed record HealthResponse(string Status);
}
