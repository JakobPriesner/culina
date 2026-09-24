using Api.Endpoints.Recipes.CookPhoto.V1;
using Api.Endpoints.Recipes.Create.V1;
using Api.Endpoints.Recipes.CreateShare.V1;
using Api.Endpoints.Recipes.Delete.V1;
using Api.Endpoints.Recipes.DrawImage.V1;
using Api.Endpoints.Recipes.GetAll.V1;
using Api.Endpoints.Recipes.GetById.V1;
using Api.Endpoints.Recipes.GetCompletions.V1;
using Api.Endpoints.Recipes.GetCookLog.V1;
using Api.Endpoints.Recipes.GetImage.V1;
using Api.Endpoints.Recipes.GetIngredients.V1;
using Api.Endpoints.Recipes.GetNotes.V1;
using Api.Endpoints.Recipes.GetShare.V1;
using Api.Endpoints.Recipes.GetTags.V1;
using Api.Endpoints.Recipes.GetUnits.V1;
using Api.Endpoints.Recipes.Import.V1;
using Api.Endpoints.Recipes.RecordCooked.V1;
using Api.Endpoints.Recipes.RemoveImage.V1;
using Api.Endpoints.Recipes.RevokeShare.V1;
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
            .AddSingleton<IEndpoint, GetTagsEndpoint>()
            .AddSingleton<IEndpoint, GetUnitsEndpoint>()
            .AddSingleton<IEndpoint, GetIngredientsEndpoint>()
            .AddSingleton<IEndpoint, GetCompletionsEndpoint>()
            .AddSingleton<IEndpoint, ImportRecipeEndpoint>()
            .AddSingleton<IEndpoint, UpdateRecipeEndpoint>()
            .AddSingleton<IEndpoint, DeleteRecipeEndpoint>()
            .AddSingleton<IEndpoint, GetNotesEndpoint>()
            .AddSingleton<IEndpoint, SaveNotesEndpoint>()
            .AddSingleton<IEndpoint, GetCookLogEndpoint>()
            .AddSingleton<IEndpoint, RecordCookedEndpoint>()
            .AddSingleton<IEndpoint, UndoCookedEndpoint>()
            .AddSingleton<IEndpoint, SetRecipeImageEndpoint>()
            .AddSingleton<IEndpoint, DrawRecipeImageEndpoint>()
            .AddSingleton<IEndpoint, RemoveRecipeImageEndpoint>()
            .AddSingleton<IEndpoint, GetRecipeImageEndpoint>()
            .AddSingleton<IEndpoint, SetCookPhotoEndpoint>()
            .AddSingleton<IEndpoint, RemoveCookPhotoEndpoint>()
            .AddSingleton<IEndpoint, GetCookPhotoEndpoint>()
            .AddSingleton<IEndpoint, GetRecipeShareEndpoint>()
            .AddSingleton<IEndpoint, CreateRecipeShareEndpoint>()
            .AddSingleton<IEndpoint, RevokeRecipeShareEndpoint>();
}
