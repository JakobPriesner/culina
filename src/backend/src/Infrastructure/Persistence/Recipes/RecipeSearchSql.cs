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
        RecipeSort.Relevance => "matched_ingredients desc, extra_ingredients asc, updated_at desc, id desc",
        _ => "updated_at desc, id desc"
    };

    /// <summary>
    /// The predicate that resumes after the cursor's row, expressed as a row
    /// comparison so PostgreSQL can use the same index the ordering does.
    /// </summary>
    internal static string? ResumePredicate(RecipeSort sort) => sort switch
    {
        RecipeSort.Title => "(title, id) > (@k0, @cursorId)",
        RecipeSort.ShortestFirst =>
            "(coalesce(total_minutes, 2147483647), id) > (coalesce(cast(nullif(@k0, '') as int), 2147483647), @cursorId)",
        RecipeSort.MostCooked => "(cook_count, id) < (cast(@k0 as int), @cursorId)",
        RecipeSort.Relevance =>
            "(matched_ingredients, -extra_ingredients, updated_at, id) "
            + "< (cast(@k0 as int), -cast(@k1 as int), cast(@k2 as timestamptz), @cursorId)",
        _ => "(updated_at, id) < (cast(@k0 as timestamptz), @cursorId)"
    };

    /// <summary>The sort key values of the last row on a page.</summary>
    internal static IReadOnlyList<string> KeysOf(RecipeSort sort, RecipeSearchRow row) => sort switch
    {
        RecipeSort.Title => [row.Title],
        RecipeSort.ShortestFirst => [RecipeCursor.Key(row.TotalMinutes)],
        RecipeSort.MostCooked => [RecipeCursor.Key(row.CookCount)],
        RecipeSort.Relevance =>
        [
            RecipeCursor.Key(row.MatchedIngredients),
            RecipeCursor.Key(row.IngredientCount - row.MatchedIngredients),
            RecipeCursor.Key(row.UpdatedAt)
        ],
        _ => [RecipeCursor.Key(row.UpdatedAt)]
    };
}
