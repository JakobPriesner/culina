using Application.Recipes.Update;
using Request = Contracts.Recipes.Update.Request;

namespace Api.Endpoints.Recipes.Update.V1;

/// <summary>Turns the request body into the command the handler accepts.</summary>
internal static class UpdateRecipeRequestExtensions
{
    internal static UpdateRecipeCommand ToCommand(this Request request, Guid recipeId, Guid userId, long version)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdateRecipeCommand(
            recipeId,
            userId,
            version,
            new RecipeDraft(
                request.Title,
                request.Description,
                request.Language,
                request.YieldAmount,
                request.YieldKind,
                request.PrepMinutes,
                request.CookMinutes,
                request.Tags),
            request.Groups,
            request.Steps);
    }
}
