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

    /// <summary>
    /// A query of <c>(recipe_id, matched)</c>: how many of <paramref name="ingredients"/> (a text array) each recipe
    /// of <paramref name="library"/> (a uuid array) holds, by the same rule as <see cref="Holds"/>.
    /// </summary>
    /// <remarks>
    /// Set-based from the ingredient side, one LIKE per word so each stays trigram-index capable. Asking
    /// <see cref="Holds"/> of every recipe instead cost five seconds for a page of cookbooks over 10,000 recipes.
    /// </remarks>
    internal static string Holders(string ingredients, string library) => $"""
        select g.recipe_id, count(distinct hit.position) as matched
        from (
            select ri.group_id, word.position
            from (
                select one.position, w.ae, w.a, count(*) over (partition by one.position) as words
                from unnest({ingredients}) with ordinality as one(name, position)
                cross join lateral culina_wanted_words(one.name) w
            ) word
            join recipe_ingredients ri on ri.name_ae like '%' || word.ae || '%'
                                       or ri.name_a like '%' || word.a || '%'
            group by ri.id, ri.group_id, word.position, word.words
            having count(*) = word.words
        ) hit
        join ingredient_groups g on g.id = hit.group_id
        join recipes owner on owner.id = g.recipe_id
                          and owner.household_id = any({library})
        group by g.recipe_id
        """;
}
