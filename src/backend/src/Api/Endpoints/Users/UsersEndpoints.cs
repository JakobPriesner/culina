using Api.Endpoints.Users.Register.V1;

namespace Api.Endpoints.Users;

/// <summary>
/// The users domain's endpoints, registered explicitly, one line each.
/// </summary>
/// <remarks>
/// Endpoints are stateless, so they are singletons. The grouping is for reading
/// only: routing matches on specificity and nothing depends on the order.
/// </remarks>
internal static class UsersEndpoints
{
    internal static IServiceCollection AddUsersEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, RegisterUserEndpoint>();
}
