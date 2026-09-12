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

    /// <summary>Deletes a recipe and everything under it.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> DeleteAsync(Guid recipeId, CancellationToken cancellationToken);
}
