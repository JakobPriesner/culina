namespace Infrastructure.Persistence.Recipes;

/// <summary>The SQL for a recipe's time.</summary>
/// <remarks>A recipe with no stated time is unknown, not instant: a time ceiling excludes it.</remarks>
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
