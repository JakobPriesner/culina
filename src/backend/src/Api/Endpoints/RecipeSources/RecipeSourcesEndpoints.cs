using Api.Endpoints.RecipeSources.Browse.V1;
using Api.Endpoints.RecipeSources.Connect.V1;
using Api.Endpoints.RecipeSources.Disconnect.V1;
using Api.Endpoints.RecipeSources.GetAll.V1;
using Api.Endpoints.RecipeSources.Import.V1;
using Api.Endpoints.RecipeSources.Watch.V1;

namespace Api.Endpoints.RecipeSources;

/// <summary>
/// The connected-library endpoints, registered explicitly.
/// </summary>
/// <remarks>
/// A resource of their own rather than a corner of <c>/recipes</c>, because a
/// connection is a thing with a lifetime: it is created, listed, used and
/// disconnected. The one-off import of a single pasted link stays where it is,
/// at <c>/recipe-imports</c>, because it creates nothing to come back to.
/// </remarks>
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
