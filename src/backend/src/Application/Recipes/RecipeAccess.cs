using Application.Abstractions;
using Domain.Households;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes;

/// <summary>
/// Answers whether the caller may see or change a recipe.
/// </summary>
/// <remarks>
/// A recipe belongs to a household, so the rule is simply "are you in it". It
/// lives here rather than in each handler so every recipe operation gives the
/// same answer — including the part that matters: a non-member is told the
/// recipe does not exist, never that it exists and is forbidden.
/// </remarks>
internal static class RecipeAccess
{
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
                .IsMemberAsync(recipe.HouseholdId, userId, cancellationToken)
                .ConfigureAwait(false)
                    ? Result<Recipe>.Success(recipe)
                    : RecipeErrors.NotFound(recipeId),
            error => Task.FromResult(Result<Recipe>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the caller is in this household at all.
    /// </summary>
    /// <remarks>
    /// The same answer for reading and for writing: a household has members,
    /// not roles, and a member may do anything in their own kitchen.
    /// </remarks>
    internal static async Task<Result> MemberOfAsync(
        IHouseholdRepository households,
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await households.IsMemberAsync(householdId, userId, cancellationToken).ConfigureAwait(false)
            ? Result.Success()
            : HouseholdErrors.NotFound(householdId);
}
