using Api.Endpoints.PasswordResetLinks.Create.V1;

namespace Api.Endpoints.PasswordResetLinks;

/// <summary>The password reset links domain's endpoints, registered explicitly.</summary>
internal static class PasswordResetLinksEndpoints
{
    internal static IServiceCollection AddPasswordResetLinksEndpoints(this IServiceCollection services) =>
        services.AddSingleton<IEndpoint, CreatePasswordResetLinkEndpoint>();
}
