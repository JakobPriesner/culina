using Api.Endpoints.EmailConfirmations.Create.V1;

namespace Api.Endpoints.EmailConfirmations;

/// <summary>The email confirmations domain's endpoints, registered explicitly.</summary>
internal static class EmailConfirmationsEndpoints
{
    internal static IServiceCollection AddEmailConfirmationsEndpoints(this IServiceCollection services) =>
        services.AddSingleton<IEndpoint, CreateEmailConfirmationEndpoint>();
}
