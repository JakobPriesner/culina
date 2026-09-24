using Api.Endpoints.Cookbooks.AddRecipe.V1;
using Api.Endpoints.Cookbooks.Create.V1;
using Api.Endpoints.Cookbooks.Delete.V1;
using Api.Endpoints.Cookbooks.GetAll.V1;
using Api.Endpoints.Cookbooks.GetById.V1;
using Api.Endpoints.Cookbooks.GetRecipes.V1;
using Api.Endpoints.Cookbooks.RemoveRecipe.V1;
using Api.Endpoints.Cookbooks.Update.V1;
using Api.Endpoints.Recipes.GetCookbooks.V1;

namespace Api.Endpoints.Cookbooks;

/// <summary>The cookbooks domain's endpoints, registered explicitly.</summary>
internal static class CookbooksEndpoints
{
    internal static IServiceCollection AddCookbooksEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetCookbooksEndpoint>()
            .AddSingleton<IEndpoint, GetCookbookEndpoint>()
            .AddSingleton<IEndpoint, GetCookbookRecipesEndpoint>()
            .AddSingleton<IEndpoint, CreateCookbookEndpoint>()
            .AddSingleton<IEndpoint, UpdateCookbookEndpoint>()
            .AddSingleton<IEndpoint, DeleteCookbookEndpoint>()
            .AddSingleton<IEndpoint, AddRecipeToCookbookEndpoint>()
            .AddSingleton<IEndpoint, RemoveRecipeFromCookbookEndpoint>()
            // Hangs off a recipe rather than a cookbook, because that is the
            // question the recipe's own page asks.
            .AddSingleton<IEndpoint, GetRecipeCookbooksEndpoint>();
}
