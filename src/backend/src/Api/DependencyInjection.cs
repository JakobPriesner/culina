using Api.Extensions;
using Api.Infrastructure;

namespace Api;

/// <summary>
/// Registers the presentation layer's own services.
/// </summary>
internal static class DependencyInjection
{
    internal static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            // ProblemDetails is required by the exception-handler middleware;
            // the body itself is written by CustomResults, so the shape stays
            // identical to every other error response.
            .AddProblemDetails()
            .AddExceptionHandler<GlobalExceptionHandler>()
            .AddCulinaRateLimiter()
            .AddRequestLogging()
            .AddCulinaJson()
            .AddCulinaOpenApi();
    }
}
