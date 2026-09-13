using Api.Endpoints.Recipes.Create.V1;
using Api.Endpoints.Recipes.Delete.V1;
using Api.Endpoints.Recipes.GetAll.V1;
using Api.Endpoints.Recipes.GetById.V1;
using Api.Endpoints.Recipes.GetCookLog.V1;
using Api.Endpoints.Recipes.GetImage.V1;
using Api.Endpoints.Recipes.GetIngredients.V1;
using Api.Endpoints.Recipes.GetNotes.V1;
using Api.Endpoints.Recipes.GetUnits.V1;
using Api.Endpoints.Recipes.RecordCooked.V1;
using Api.Endpoints.Recipes.RemoveImage.V1;
using Api.Endpoints.Recipes.SaveNotes.V1;
using Api.Endpoints.Recipes.SetImage.V1;
using Api.Endpoints.Recipes.UndoCooked.V1;
using Api.Endpoints.Recipes.Update.V1;

namespace Api.Endpoints.Recipes;

/// <summary>The recipes domain's endpoints, registered explicitly.</summary>
internal static class RecipesEndpoints
{
    internal static IServiceCollection AddRecipesEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, CreateRecipeEndpoint>()
            .AddSingleton<IEndpoint, GetRecipeEndpoint>()
            .AddSingleton<IEndpoint, GetRecipesEndpoint>()
            .AddSingleton<IEndpoint, GetUnitsEndpoint>()
            .AddSingleton<IEndpoint, GetIngredientsEndpoint>()
            .AddSingleton<IEndpoint, UpdateRecipeEndpoint>()
            .AddSingleton<IEndpoint, DeleteRecipeEndpoint>()
            .AddSingleton<IEndpoint, GetNotesEndpoint>()
            .AddSingleton<IEndpoint, SaveNotesEndpoint>()
            .AddSingleton<IEndpoint, GetCookLogEndpoint>()
            .AddSingleton<IEndpoint, RecordCookedEndpoint>()
            .AddSingleton<IEndpoint, UndoCookedEndpoint>()
            .AddSingleton<IEndpoint, SetRecipeImageEndpoint>()
            .AddSingleton<IEndpoint, RemoveRecipeImageEndpoint>()
            .AddSingleton<IEndpoint, GetRecipeImageEndpoint>();
}
