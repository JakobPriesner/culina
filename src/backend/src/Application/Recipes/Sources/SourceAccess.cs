using Application.Abstractions;
using Domain.Import;
using Domain.Shared;

namespace Application.Recipes.Sources;

/// <summary>Answers whether the caller may use a connection: the household rule recipes have, mattering more as a connection holds a credential.</summary>
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
                    // Not found, never forbidden: a stranger learns nothing about which kitchens connected what.
                    : ImportErrors.SourceNotFound(sourceId),
            error => Task.FromResult(Result<RecipeSource>.Failure(error))).ConfigureAwait(false);
    }

    // A usable connection that can be read from. One whose token can no longer be decrypted is refused here only, so disconnecting it (the fix) still works.
    internal static async Task<Result<RecipeSource>> ReadableAsync(
        IRecipeSourceRepository sources,
        IHouseholdRepository households,
        Guid sourceId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var usable = await UsableAsync(sources, households, sourceId, userId, cancellationToken)
            .ConfigureAwait(false);

        return usable.Bind(source => source.NeedsReconnecting
            ? Result<RecipeSource>.Failure(ImportErrors.SourceNeedsReconnecting)
            : source);
    }
}
