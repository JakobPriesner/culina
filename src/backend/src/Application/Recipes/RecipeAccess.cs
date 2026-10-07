using Application.Abstractions;
using Application.Households;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes;

/// <summary>Answers whether the caller may see or change a recipe.</summary>
/// <remarks>
/// Members of the owning household may do anything; members of a household that inherits it may
/// read, cook, plan and shop for it. A caller who may not see a recipe is told it does not exist,
/// never that it is forbidden. Reading asks <see cref="VisibleAsync"/>, changing it
/// <see cref="EditableAsync"/>, using it inside one household (plan, shelf, cook log)
/// <see cref="VisibleInAsync"/>.
/// </remarks>
internal static class RecipeAccess
{
    /// <summary>
    /// Whether the caller may read the recipe: they are in its household, or in one that inherits
    /// from it.
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
    /// <see cref="VisibleAsync"/> without loading the recipe: its household, or not-found.
    /// </summary>
    internal static async Task<Result<Guid>> VisibleHouseholdAsync(
        IRecipeRepository recipes,
        IHouseholdRepository households,
        Guid recipeId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var owner = await recipes.HouseholdOfAsync(recipeId, cancellationToken).ConfigureAwait(false);

        return await owner.Match(
            async household => await households
                .CanSeeRecipesAsync(household, userId, cancellationToken)
                .ConfigureAwait(false)
                    ? Result<Guid>.Success(household)
                    : RecipeErrors.NotFound(recipeId),
            error => Task.FromResult(Result<Guid>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary><see cref="EditableAsync"/> without loading the recipe.</summary>
    internal static async Task<Result<Guid>> EditableHouseholdAsync(
        IRecipeRepository recipes,
        IHouseholdRepository households,
        Guid recipeId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var owner = await recipes.HouseholdOfAsync(recipeId, cancellationToken).ConfigureAwait(false);

        return await owner.Match(
            async household => await households
                .IsMemberAsync(household, userId, cancellationToken)
                .ConfigureAwait(false)
                    ? Result<Guid>.Success(household)
                    : RecipeErrors.NotFound(recipeId),
            error => Task.FromResult(Result<Guid>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the caller may change the recipe: they are in the household it belongs to;
    /// inheriting is not enough.
    /// </summary>
    /// <remarks>
    /// Still not-found, not forbidden, for somebody who can only read it: the client that offered
    /// them the edit is the thing to fix.
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
    /// Whether the caller may use the recipe in one household: they are in it, and the recipe is
    /// its own or inherited. A null household means the recipe's own.
    /// </summary>
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
