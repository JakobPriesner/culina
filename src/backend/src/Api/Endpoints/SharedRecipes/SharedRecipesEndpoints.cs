using Api.Endpoints.SharedRecipes.GetById.V1;
using Api.Endpoints.SharedRecipes.GetImage.V1;

namespace Api.Endpoints.SharedRecipes;

/// <summary>The shared-recipes endpoints, registered explicitly.</summary>
/// <remarks>
/// Two reads and nothing else, deliberately. Everything a stranger holding a
/// link may do is listed here, on one screen.
/// </remarks>
internal static class SharedRecipesEndpoints
{
    internal static IServiceCollection AddSharedRecipesEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetSharedRecipeEndpoint>()
            .AddSingleton<IEndpoint, GetSharedRecipeImageEndpoint>();
}
