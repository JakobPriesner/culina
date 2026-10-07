namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// The SQL every query that reads a recipe's time says the same way.
/// </summary>
/// <remarks>
/// A recipe with no stated time is unknown, not instant: it has no total, and a
/// time ceiling excludes it rather than counting it as zero minutes. That rule
/// was written out in ten places, so a change to it was ten edits and a miss
/// was two screens disagreeing about whether a recipe fits.
/// </remarks>
internal static class RecipeSql
{
    /// <summary>Prep plus cook, or null when neither is stated.</summary>
    /// <param name="recipe">The alias of the recipes table in the query.</param>
    internal static string TotalMinutes(string recipe = "r") => $"""
        case when {recipe}.prep_minutes is null and {recipe}.cook_minutes is null then null
             else coalesce({recipe}.prep_minutes, 0) + coalesce({recipe}.cook_minutes, 0) end
        """;

    /// <summary>A condition: the recipe states a time, and it is at most the limit.</summary>
    /// <param name="limit">An expression: a parameter, or a column.</param>
    /// <param name="recipe">The alias of the recipes table in the query.</param>
    internal static string FitsWithin(string limit, string recipe = "r") => $"""
        (({recipe}.prep_minutes is not null or {recipe}.cook_minutes is not null)
         and coalesce({recipe}.prep_minutes, 0) + coalesce({recipe}.cook_minutes, 0) <= {limit})
        """;
}
