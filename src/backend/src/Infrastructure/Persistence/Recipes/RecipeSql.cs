namespace Infrastructure.Persistence.Recipes;

/// <summary>The SQL for a recipe's time.</summary>
/// <remarks>
/// A recipe with no stated time is unknown, not instant: a time ceiling excludes it.
/// </remarks>
internal static class RecipeSql
{
    /// <summary>
    /// Prep plus cook, or null when neither is stated; <paramref name="recipe"/> is the recipes
    /// table alias.
    /// </summary>
    internal static string TotalMinutes(string recipe = "r") => $"""
        case when {recipe}.prep_minutes is null and {recipe}.cook_minutes is null then null
             else coalesce({recipe}.prep_minutes, 0) + coalesce({recipe}.cook_minutes, 0) end
        """;

    /// <summary>
    /// A condition: the recipe states a time at most <paramref name="limit"/> (a parameter or
    /// column expression); <paramref name="recipe"/> is the table alias.
    /// </summary>
    internal static string FitsWithin(string limit, string recipe = "r") => $"""
        (({recipe}.prep_minutes is not null or {recipe}.cook_minutes is not null)
         and coalesce({recipe}.prep_minutes, 0) + coalesce({recipe}.cook_minutes, 0) <= {limit})
        """;
}
