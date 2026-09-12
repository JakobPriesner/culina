using Domain.Recipes;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes recipes.</summary>
public interface IRecipeRepository
{
    /// <summary>Loads a recipe with its groups, ingredients and steps.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<Recipe>> FindAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>Finds matching recipes, one page at a time.</summary>
    /// <param name="search">What to look for.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<RecipePage> SearchAsync(RecipeSearch search, CancellationToken cancellationToken);

    /// <summary>Stores a new recipe.</summary>
    /// <param name="recipe">The recipe to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> AddAsync(Recipe recipe, CancellationToken cancellationToken);

    /// <summary>Saves a changed recipe, contents included.</summary>
    /// <param name="recipe">The changed recipe.</param>
    /// <param name="expectedVersion">The version the caller last saw.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result<long>> UpdateAsync(
        Recipe recipe,
        long expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>Attaches a stored image to a recipe, replacing any previous one.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="image">What was stored.</param>
    /// <param name="contentType">The media type of the renditions.</param>
    /// <param name="now">The injected current time.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>What this image replaced, if anything.</returns>
    Task<Result<ImageReplacement>> SetImageAsync(
        Guid recipeId,
        StoredImage image,
        string contentType,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Removes a recipe's image.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="now">The injected current time.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>What was removed, if anything.</returns>
    Task<Result<ImageReplacement>> RemoveImageAsync(
        Guid recipeId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>The content hash of a recipe's image, for serving it.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<string>> ImageHashAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>Deletes a recipe and everything under it.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> DeleteAsync(Guid recipeId, CancellationToken cancellationToken);
}
