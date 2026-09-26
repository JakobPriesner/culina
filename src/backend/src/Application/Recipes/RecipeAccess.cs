using Application.Abstractions;
using Application.Households;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes;

/// <summary>
/// Answers whether the caller may see or change a recipe.
/// </summary>
/// <remarks>
/// <para>
/// A recipe belongs to one household. Its members may do anything with it;
/// members of a household that inherits from it may read it, cook it, plan it
/// and shop for it, but not change it. The rules live here rather than in each
/// handler so every recipe operation gives the same answer — including the part
/// that matters: a caller who may not see a recipe is told it does not exist,
/// never that it exists and is forbidden.
/// </para>
/// <para>
/// Three questions, because there are three kinds of operation. Reading asks
/// <see cref="VisibleAsync"/>. Changing the recipe itself asks
/// <see cref="EditableAsync"/>. Doing something with it inside one particular
/// household — planning it, shelving it, cooking it there — asks
/// <see cref="VisibleInAsync"/>, because the household that inherits is the one
/// whose plan, shelf or cook log is being written.
/// </para>
/// </remarks>
internal static class RecipeAccess
{
    /// <summary>
    /// Whether the caller may read the recipe: they are in its household, or
    /// in one that inherits from it.
    /// </summary>
    internal static async Task<Result<Recipe>> VisibleAsync(
        IRecipeRepository recipes,
        IHouseholdRepository households,
        Guid recipeId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var found = await recipes.FindAsync(recipeId, cancellationToken).ConfigureAwait(false);

        return await found.Match(
            async recipe => await households
                .CanSeeRecipesAsync(recipe.HouseholdId, userId, cancellationToken)
                .ConfigureAwait(false)
                    ? Result<Recipe>.Success(recipe)
                    : RecipeErrors.NotFound(recipeId),
            error => Task.FromResult(Result<Recipe>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the caller may change the recipe: they are in the household it
    /// belongs to. Inheriting it is not enough.
    /// </summary>
    /// <remarks>
    /// Still not-found rather than forbidden for somebody who can read it
    /// through inheritance: the recipe they may change does not exist, and a
    /// client that offered them the edit is the thing to fix.
    /// </remarks>
    internal static async Task<Result<Recipe>> EditableAsync(
        IRecipeRepository recipes,
        IHouseholdRepository households,
        Guid recipeId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var found = await recipes.FindAsync(recipeId, cancellationToken).ConfigureAwait(false);

        return await found.Match(
            async recipe => await households
                .IsMemberAsync(recipe.HouseholdId, userId, cancellationToken)
                .ConfigureAwait(false)
                    ? Result<Recipe>.Success(recipe)
                    : RecipeErrors.NotFound(recipeId),
            error => Task.FromResult(Result<Recipe>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the caller may use the recipe in one household: they are in
    /// that household, and the recipe is its own or one it inherits.
    /// </summary>
    /// <param name="recipes">The recipe repository.</param>
    /// <param name="households">The household repository.</param>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="householdId">
    /// The household it is being used in, or null for the recipe's own.
    /// </param>
    /// <param name="userId">Who is asking.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    internal static async Task<Result<Recipe>> VisibleInAsync(
        IRecipeRepository recipes,
        IHouseholdRepository households,
        Guid recipeId,
        Guid? householdId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var found = await recipes.FindAsync(recipeId, cancellationToken).ConfigureAwait(false);

        return await found.Match(
            async recipe =>
            {
                var kitchen = householdId ?? recipe.HouseholdId;

                if (!await households.IsMemberAsync(kitchen, userId, cancellationToken).ConfigureAwait(false))
                {
                    return RecipeErrors.NotFound(recipeId);
                }

                var library = await HouseholdAccess
                    .LibraryAsync(households, kitchen, cancellationToken)
                    .ConfigureAwait(false);

                return library.Contains(recipe.HouseholdId)
                    ? Result<Recipe>.Success(recipe)
                    : RecipeErrors.NotFound(recipeId);
            },
            error => Task.FromResult(Result<Recipe>.Failure(error))).ConfigureAwait(false);
    }
}
