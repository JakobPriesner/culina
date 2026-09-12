using Api.Endpoints.Registration.GetPolicy.V1;

namespace Api.Endpoints.Registration;

/// <summary>The public registration endpoints, registered explicitly.</summary>
internal static class RegistrationEndpoints
{
    internal static IServiceCollection AddRegistrationEndpoints(this IServiceCollection services) =>
        services.AddSingleton<IEndpoint, GetRegistrationPolicyEndpoint>();
}
