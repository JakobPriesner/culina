using Api.Endpoints.CookSessions.End.V1;
using Api.Endpoints.CookSessions.GetCurrent.V1;
using Api.Endpoints.CookSessions.Start.V1;
using Api.Endpoints.CookSessions.Update.V1;

namespace Api.Endpoints.CookSessions;

/// <summary>The cooking-session endpoints, registered explicitly.</summary>
internal static class CookSessionEndpoints
{
    internal static IServiceCollection AddCookSessionEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, StartCookSessionEndpoint>()
            .AddSingleton<IEndpoint, GetCurrentCookSessionEndpoint>()
            .AddSingleton<IEndpoint, UpdateCookSessionEndpoint>()
            .AddSingleton<IEndpoint, EndCookSessionEndpoint>();
}
