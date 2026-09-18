using Application.Abstractions;

namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// The ordering half of recipe search: what each sort orders by, and how a
/// cursor resumes it.
/// </summary>
/// <remarks>
/// Kept apart from the filtering so each can be read on its own. Every order
/// ends in the recipe id, which is what makes paging stable — see
/// <see cref="RecipeCursor"/>.
/// </remarks>
internal static class RecipeSearchSql
{
    /// <summary>The <c>order by</c> clause for a sort.</summary>
    internal static string OrderBy(RecipeSort sort) => sort switch
    {
        RecipeSort.Title => "title asc, id asc",
        // Nulls last: a recipe with no stated time is not "the quickest", it is
        // simply unknown, and putting it first would be a lie.
        RecipeSort.ShortestFirst => "total_minutes asc nulls last, id asc",
        RecipeSort.MostCooked => "cook_count desc, id desc",
        // The tier before the score, always. Evidence of a better kind outranks
        // more of a worse kind, so a recipe that merely resembles the query
        // cannot climb past one the query names — however the weights inside
        // the score are tuned later.
        RecipeSort.Relevance => "tier asc, score desc, updated_at desc, id desc",
        // Oldest first: a cookbook reads like a table of contents, and the
        // order somebody built it in is the order they meant.
        RecipeSort.CookbookOrder => "added_to_cookbook_at asc, id asc",
        _ => "updated_at desc, id desc"
    };

    /// <summary>
    /// The predicate that resumes after the cursor's row, expressed as a row
    /// comparison so PostgreSQL can use the same index the ordering does.
    /// </summary>
    /// <remarks>
    /// A row comparison reads every component in one direction, so the one
    /// ascending key in the relevance order — the tier — is negated rather
    /// than the four descending ones. Ordering by <c>tier asc</c> and by
    /// <c>-tier desc</c> are the same order, and the second is the one that
    /// fits in a single <c>&lt;</c>.
    /// </remarks>
    internal static string? ResumePredicate(RecipeSort sort) => sort switch
    {
        RecipeSort.Title => "(title, id) > (@k0, @cursorId)",
        RecipeSort.ShortestFirst =>
            "(coalesce(total_minutes, 2147483647), id) > (coalesce(cast(nullif(@k0, '') as int), 2147483647), @cursorId)",
        RecipeSort.MostCooked => "(cook_count, id) < (cast(@k0 as int), @cursorId)",
        RecipeSort.Relevance =>
            "(-tier, score, updated_at, id) "
            + "< (-cast(@k0 as int), cast(@k1 as float8), cast(@k2 as timestamptz), @cursorId)",
        RecipeSort.CookbookOrder =>
            "(added_to_cookbook_at, id) > (cast(@k0 as timestamptz), @cursorId)",
        _ => "(updated_at, id) < (cast(@k0 as timestamptz), @cursorId)"
    };

    /// <summary>
    /// The sort key values of the last row on a page.
    /// </summary>
    /// <remarks>
    /// Reads the row as PostgreSQL returned it rather than the mapped result,
    /// because the tier and the score are how a page resumes and are nobody
    /// else's business: putting them on the Application row would publish two
    /// tuning numbers to every caller of the recipe list.
    /// </remarks>
    internal static IReadOnlyList<string> KeysOf(RecipeSort sort, RecipeSearchRowData row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return sort switch
        {
            RecipeSort.Title => [row.Title],
            RecipeSort.ShortestFirst => [RecipeCursor.Key(row.TotalMinutes)],
            RecipeSort.MostCooked => [RecipeCursor.Key(row.CookCount)],
            RecipeSort.Relevance =>
            [
                RecipeCursor.Key(row.Tier),
                RecipeCursor.Key(row.Score),
                RecipeCursor.Key(row.UpdatedAt)
            ],
            RecipeSort.CookbookOrder => [RecipeCursor.Key(row.AddedToCookbookAt ?? row.UpdatedAt)],
            _ => [RecipeCursor.Key(row.UpdatedAt)]
        };
    }
}
