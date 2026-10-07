using Application.Abstractions;

namespace Infrastructure.Persistence.Recipes;

/// <summary>The ordering half of recipe search: what each sort orders by and how a cursor resumes it.</summary>
/// <remarks>Every order ends in the recipe id, which makes paging stable (see <see cref="RecipeCursor"/>).</remarks>
internal static class RecipeSearchSql
{
    /// <summary>The <c>order by</c> clause for a sort.</summary>
    internal static string OrderBy(RecipeSort sort) => sort switch
    {
        RecipeSort.Title => "title asc, id asc",
        // Nulls last: an unstated time is unknown, not the quickest.
        RecipeSort.ShortestFirst => "total_minutes asc nulls last, id asc",
        RecipeSort.MostCooked => "cook_count desc, id desc",
        // Tier before score, always: better evidence outranks more of a worse kind, however weights are tuned.
        RecipeSort.Relevance => "tier asc, score desc, updated_at desc, id desc",
        // Oldest first: a cookbook reads like a table of contents in the order it was built.
        RecipeSort.CookbookOrder => "added_to_cookbook_at asc, id asc",
        // The score is a function of the day, so the second page resumes the order the first was cut from.
        RecipeSort.Suggested => "suggestion_score desc, id desc",
        _ => "updated_at desc, id desc"
    };

    // A row comparison reads every component in one direction, so the one ascending key (tier) is negated:
    // "tier asc" equals "-tier desc", which fits a single "<".
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
        RecipeSort.Suggested => "(suggestion_score, id) < (cast(@k0 as numeric), @cursorId)",
        _ => "(updated_at, id) < (cast(@k0 as timestamptz), @cursorId)"
    };

    // Reads the row as PostgreSQL returned it: tier and score are for resuming a page and must not be published on the Application row.
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
            RecipeSort.Suggested => [RecipeCursor.Key(row.SuggestionScore)],
            _ => [RecipeCursor.Key(row.UpdatedAt)]
        };
    }
}
