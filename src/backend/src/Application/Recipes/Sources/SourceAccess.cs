using Application.Abstractions;
using Domain.Import;
using Domain.Shared;

namespace Application.Recipes.Sources;

/// <summary>
/// Answers whether the caller may use a connection.
/// </summary>
/// <remarks>
/// A connection belongs to a household, so the rule is the same one recipes
/// have: are you in it. It matters more here than elsewhere, because a
/// connection holds a credential and can be made to fetch — being able to use
/// somebody else's connection would be being able to read their Tandoor.
/// </remarks>
internal static class SourceAccess
{
    internal static async Task<Result<RecipeSource>> UsableAsync(
        IRecipeSourceRepository sources,
        IHouseholdRepository households,
        Guid sourceId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var found = await sources.FindAsync(sourceId, cancellationToken).ConfigureAwait(false);

        return await found.Match(
            async source => await households
                .IsMemberAsync(source.HouseholdId, userId, cancellationToken)
                .ConfigureAwait(false)
                    ? Result<RecipeSource>.Success(source)
                    // Not found, never forbidden: a stranger learns nothing
                    // about which kitchens have connected what.
                    : ImportErrors.SourceNotFound(sourceId),
            error => Task.FromResult(Result<RecipeSource>.Failure(error))).ConfigureAwait(false);
    }
}
