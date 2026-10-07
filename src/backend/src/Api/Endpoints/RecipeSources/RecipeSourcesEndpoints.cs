using Api.Endpoints.RecipeSources.Browse.V1;
using Api.Endpoints.RecipeSources.Connect.V1;
using Api.Endpoints.RecipeSources.Disconnect.V1;
using Api.Endpoints.RecipeSources.GetAll.V1;
using Api.Endpoints.RecipeSources.Import.V1;
using Api.Endpoints.RecipeSources.Watch.V1;

namespace Api.Endpoints.RecipeSources;

/// <summary>The connected-library endpoints: a resource with a lifetime, unlike the one-off <c>/recipe-imports</c> of a pasted link.</summary>
internal static class RecipeSourcesEndpoints
{
    internal static IServiceCollection AddRecipeSourcesEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, ConnectRecipeSourceEndpoint>()
            .AddSingleton<IEndpoint, GetRecipeSourcesEndpoint>()
            .AddSingleton<IEndpoint, DisconnectRecipeSourceEndpoint>()
            .AddSingleton<IEndpoint, BrowseRecipeSourceEndpoint>()
            .AddSingleton<IEndpoint, ImportFromRecipeSourceEndpoint>()
            .AddSingleton<IEndpoint, WatchRecipeImportEndpoint>();
}
