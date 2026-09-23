namespace Infrastructure.Persistence.Cookbooks;

/// <summary>
/// What it means for a recipe to be on a smart shelf, as one SQL predicate.
/// </summary>
/// <remarks>
/// <para>
/// The question is asked from two places — the card, which counts what is on a
/// shelf and draws its cover, and the recipe list, which is what you get when
/// you open that shelf. They were written out separately and stayed identical
/// by hand, which is a promise nothing could keep: a fourth rule, or a change
/// to how an ingredient is matched, would have left the card saying "12
/// recipes" over a list of nine, with no test failing.
/// </para>
/// <para>
/// The two callers differ only in where the values come from — a cookbook row
/// for the card, query parameters for the list — so that is all this takes.
/// </para>
/// </remarks>
internal static class SmartShelfSql
{
    /// <summary>
    /// Every rule must hold, and each is the same clause the filter bar
    /// already uses, so a shelf and a search cannot come to different
    /// conclusions about the same words.
    /// </summary>
    /// <param name="tags">The SQL expression holding the required tag slugs.</param>
    /// <param name="ingredients">The SQL expression holding the required ingredients.</param>
    /// <param name="maxMinutes">The SQL expression holding the time ceiling.</param>
    /// <param name="held">
    /// How many of <paramref name="ingredients"/> this recipe has, when the
    /// caller has already worked that out. Defaults to asking per recipe.
    /// </param>
    internal static string Matches(
        string tags,
        string ingredients,
        string maxMinutes,
        string? held = null) => $"""
            (cardinality({tags}) = 0 or (
                select count(distinct t.slug) from recipe_tags rt
                join tags t on t.id = rt.tag_id
                where rt.recipe_id = r.id and t.slug = any ({tags}))
                = cardinality({tags}))
            -- Unlike the ingredient ranking the search does, this excludes. On
            -- a shelf asking for chicken, a recipe without chicken is not a
            -- worse match; it is not on the shelf.
            and (cardinality({ingredients}) = 0
                 or {held ?? Held(ingredients)} = cardinality({ingredients}))
            -- A recipe with no stated time is excluded by the ceiling rather
            -- than treated as taking zero minutes, the same way the filter bar
            -- reads it.
            and ({maxMinutes} is null
                 or ((r.prep_minutes is not null or r.cook_minutes is not null)
                     and coalesce(r.prep_minutes, 0) + coalesce(r.cook_minutes, 0)
                         <= {maxMinutes}))
        """;

    /// <summary>
    /// How many of the required ingredients one recipe has, asked per recipe.
    /// </summary>
    /// <remarks>
    /// The only form the cookbook card can use: every shelf on that page
    /// carries its own rules, so there is nothing to work out once for all of
    /// them. The recipe list has one rule for the whole query and passes a
    /// count it has already aggregated — the same number, reached without
    /// asking again for every recipe in the household.
    /// </remarks>
    private static string Held(string ingredients) => $"""
        (select count(*) from unnest({ingredients}) as required
         where exists (
             select 1 from recipe_ingredients ri
             join ingredient_groups g on g.id = ri.group_id
             where g.recipe_id = r.id and ri.name ilike '%' || required || '%'))
        """;
}
