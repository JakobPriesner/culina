using Application.Abstractions;
using Domain.Cookbooks;
using Domain.Shared;

namespace Application.Cookbooks;

/// <summary>What reading "inside a cookbook" means: a manual shelf names rows, a smart one names conditions.</summary>
/// <param name="Membership">The shelf whose rows to join, or null.</param>
/// <param name="Rules">The conditions to apply, or null.</param>
/// <param name="Ordered">Whether the shelf has a chosen order; only manual shelves do, smart ones fall back to newest first.</param>
internal sealed record CookbookScope(Guid? Membership, RecipeRules? Rules, bool Ordered)
{
    /// <summary>Reading the whole collection.</summary>
    internal static readonly CookbookScope Everything = new(null, null, Ordered: false);

    /// <summary>Works out how to read the named cookbook.</summary>
    /// <param name="cookbooks">Where shelves are stored.</param>
    /// <param name="cookbookId">Which shelf, or null for everything.</param>
    /// <param name="householdId">The household being read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <remarks>
    /// An unknown or foreign shelf resolves to one that matches nothing, not a failure. The household is checked here so a
    /// smart shelf's rules from another household are never applied to the caller's recipes.
    /// </remarks>
    internal static async Task<CookbookScope> ResolveAsync(
        ICookbookRepository cookbooks,
        Guid? cookbookId,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        if (cookbookId is not { } id)
        {
            return Everything;
        }

        var found = await cookbooks.FindAsync(id, cancellationToken).ConfigureAwait(false);

        // A shelf this household may not read behaves like a missing one: empty, not the whole library.
        var matchesNothing = new CookbookScope(id, null, Ordered: true);

        return found.Match(
            cookbook => cookbook.HouseholdId == householdId ? For(cookbook) : matchesNothing,
            _ => matchesNothing);
    }

    private static CookbookScope For(Cookbook cookbook)
    {
        if (cookbook.Kind != CookbookKind.Smart)
        {
            return new CookbookScope(cookbook.Id, null, Ordered: true);
        }

        return new CookbookScope(
            null,
            new RecipeRules(
                cookbook.Rules.Tags,
                cookbook.Rules.Ingredients,
                cookbook.Rules.MaxMinutes),
            Ordered: false);
    }
}
