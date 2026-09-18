using Domain.Recipes;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes the links that publish a recipe.</summary>
public interface IRecipeShareRepository
{
    /// <summary>The link this recipe already has, if it has one.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<RecipeShare>> FindAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>Which recipe a link leads to.</summary>
    /// <param name="token">The secret out of the link.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<RecipeShare>> FindByTokenAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes the recipe, or returns the link it already had.
    /// </summary>
    /// <param name="share">The link to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// Idempotent on purpose: pressing share twice, or a retried request, must
    /// hand back the same address rather than replace one that has already been
    /// sent to somebody.
    /// </remarks>
    Task<Result<RecipeShare>> AddOrKeepAsync(RecipeShare share, CancellationToken cancellationToken);

    /// <summary>Takes the link back.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> RemoveAsync(Guid recipeId, CancellationToken cancellationToken);
}
