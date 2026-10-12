namespace Infrastructure.Persistence.Recipes;

/// <summary>The SQL for whether a recipe's ingredient is one somebody asked for.</summary>
/// <remarks>
/// Every word of what was asked for, without its ending, somewhere in the name in either fold (migration 0038):
/// "grüner Spargel" holds "Spargel, grün" and "Grünspargel", "Tomaten" holds "Tomate", "Kokosmilch" holds
/// "Kokosmilch light" but not the other way round.
/// </remarks>
internal static class IngredientSql
{
    /// <summary>
    /// A condition: the <c>recipe_ingredients</c> row <paramref name="ingredient"/> (a table alias) holds
    /// <paramref name="wanted"/> (a text expression).
    /// </summary>
    internal static string Holds(string ingredient, string wanted) => $"""
        not exists (
            select 1 from culina_wanted_words({wanted}) w
            where position(w.ae in {ingredient}.name_ae) = 0 and position(w.a in {ingredient}.name_a) = 0)
        """;
}
