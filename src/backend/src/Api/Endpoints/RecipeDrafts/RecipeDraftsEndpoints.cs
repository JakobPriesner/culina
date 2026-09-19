using Api.Endpoints.RecipeDrafts.Compose.V1;

namespace Api.Endpoints.RecipeDrafts;

/// <summary>The assistant's recipe endpoints, registered explicitly.</summary>
internal static class RecipeDraftsEndpoints
{
    internal static IServiceCollection AddRecipeDraftsEndpoints(this IServiceCollection services) =>
        services.AddSingleton<IEndpoint, ComposeRecipeDraftEndpoint>();
}
