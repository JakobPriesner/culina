using Domain.Recipes;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes the links that publish a recipe.</summary>
public interface IRecipeShareRepository
{
    /// <summary>The link this recipe already has, if it has one.</summary>
    Task<Result<RecipeShare>> FindAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>Which recipe a link leads to.</summary>
    Task<Result<RecipeShare>> FindByTokenAsync(string token, CancellationToken cancellationToken);

    /// <summary>Publishes the recipe, or returns the link it already had.</summary>
    /// <remarks>
    /// Idempotent: a double press or retry must not replace an address already sent to somebody.
    /// </remarks>
    Task<Result<RecipeShare>> AddOrKeepAsync(RecipeShare share, CancellationToken cancellationToken);

    /// <summary>Takes the link back.</summary>
    Task<Result> RemoveAsync(Guid recipeId, CancellationToken cancellationToken);
}
