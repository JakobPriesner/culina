using Application.Abstractions;
using Domain.Cookbooks;
using Domain.Shared;

namespace Application.Cookbooks;

/// <summary>
/// What reading "inside a cookbook" means for a particular cookbook.
/// </summary>
/// <remarks>
/// The two kinds of shelf are the same screen and the same query, and they
/// differ in one clause: a manual one names rows somebody wrote into
/// <c>cookbook_recipes</c>, and a smart one names conditions. Resolving that
/// here keeps the recipe searcher from having to know which it is looking at,
/// and keeps the difference in one place rather than in every caller.
/// </remarks>
/// <param name="Membership">The shelf whose rows to join, or null.</param>
/// <param name="Rules">The conditions to apply, or null.</param>
/// <param name="Ordered">
/// Whether the shelf has an order somebody chose. Only a manual one does — a
/// smart shelf was never put in an order, so it falls back to newest first.
/// </param>
internal sealed record CookbookScope(Guid? Membership, RecipeRules? Rules, bool Ordered)
{
    /// <summary>Reading the whole collection.</summary>
    internal static readonly CookbookScope Everything = new(null, null, Ordered: false);

    /// <summary>
    /// Works out how to read the named cookbook.
    /// </summary>
    /// <param name="cookbooks">Where shelves are stored.</param>
    /// <param name="cookbookId">Which shelf, or null for everything.</param>
    /// <param name="householdId">The household being read.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <remarks>
    /// <para>
    /// A shelf that does not exist, or belongs to another household, resolves
    /// to one that matches nothing rather than to a failure. <c>cookbookId</c>
    /// is a filter value and not a resource named in the path, so it behaves
    /// the way an unknown tag slug already does — and the cookbook's own page
    /// reads <c>GET /cookbooks/{id}</c> for its header, which does answer 404.
    /// </para>
    /// <para>
    /// The household is checked here rather than left to the join. A manual
    /// shelf would be emptied by the join anyway, because its membership rows
    /// belong to the other household — but a smart one carries conditions, and
    /// conditions copied out of a foreign shelf would be applied to the
    /// caller's own recipes and answer which of them match.
    /// </para>
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

        // A shelf this household may not read is treated exactly like one that
        // is not there: a membership scope whose join matches nothing, so the
        // page comes back empty rather than as the whole library.
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
