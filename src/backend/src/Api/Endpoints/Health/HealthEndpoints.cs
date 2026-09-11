using Application.Abstractions;

namespace Api.Endpoints.Health;

/// <summary>
/// Liveness and readiness.
/// </summary>
/// <remarks>
/// Neither lives under <c>/api</c>, neither requires authentication, and both
/// are filtered out of tracing — they are most of the traffic and none of the
/// information.
/// </remarks>
internal static class HealthEndpoints
{
    internal static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Liveness answers "is the process running", and must touch nothing
        // else: a dependency failure here would make an orchestrator restart a
        // process that is working fine.
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

    private sealed record HealthResponse(string Status);
}
