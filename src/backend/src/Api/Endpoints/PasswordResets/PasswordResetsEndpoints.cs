using Api.Endpoints.PasswordResets.Create.V1;

namespace Api.Endpoints.PasswordResets;

/// <summary>The password resets domain's endpoints, registered explicitly.</summary>
internal static class PasswordResetsEndpoints
{
    internal static IServiceCollection AddPasswordResetsEndpoints(this IServiceCollection services) =>
        services.AddSingleton<IEndpoint, CreatePasswordResetEndpoint>();
}
