using Api.Endpoints.Recipes.Create.V1;
using Api.Endpoints.Recipes.Delete.V1;
using Api.Endpoints.Recipes.GetById.V1;
using Api.Endpoints.Recipes.Update.V1;

namespace Api.Endpoints.Recipes;

/// <summary>The recipes domain's endpoints, registered explicitly.</summary>
internal static class RecipesEndpoints
{
    internal static IServiceCollection AddRecipesEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, CreateRecipeEndpoint>()
            .AddSingleton<IEndpoint, GetRecipeEndpoint>()
            .AddSingleton<IEndpoint, UpdateRecipeEndpoint>()
            .AddSingleton<IEndpoint, DeleteRecipeEndpoint>();
}
