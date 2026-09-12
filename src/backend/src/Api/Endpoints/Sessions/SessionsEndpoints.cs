using Api.Endpoints.Sessions.GetAll.V1;
using Api.Endpoints.Sessions.Revoke.V1;
using Api.Endpoints.Sessions.SignIn.V1;
using Api.Endpoints.Sessions.SignOut.V1;

namespace Api.Endpoints.Sessions;

/// <summary>The sessions domain's endpoints, registered explicitly.</summary>
internal static class SessionsEndpoints
{
    internal static IServiceCollection AddSessionsEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, SignInEndpoint>()
            .AddSingleton<IEndpoint, SignOutEndpoint>()
            .AddSingleton<IEndpoint, GetSessionsEndpoint>()
            .AddSingleton<IEndpoint, RevokeSessionEndpoint>();
}
