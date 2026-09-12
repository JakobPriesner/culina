using Api.Endpoints;
using Api.Endpoints.Users;

namespace Api.Extensions;

/// <summary>
/// Collects and maps every endpoint.
/// </summary>
internal static class EndpointExtensions
{
    /// <summary>
    /// Registers each domain's endpoints. One line per domain, in folder order;
    /// nothing depends on the order itself.
    /// </summary>
    internal static IServiceCollection AddEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddUsersEndpoints();
    }

    /// <summary>Maps everything registered as an <see cref="IEndpoint"/>.</summary>
    internal static WebApplication MapEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        foreach (var endpoint in app.Services.GetServices<IEndpoint>())
        {
            endpoint.MapEndpoint(app);
        }

        return app;
    }
}
