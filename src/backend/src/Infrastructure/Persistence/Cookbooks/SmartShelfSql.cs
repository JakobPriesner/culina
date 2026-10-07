using Infrastructure.Persistence.Recipes;

namespace Infrastructure.Persistence.Cookbooks;

/// <summary>
/// What it means for a recipe to be on a smart shelf, as one SQL predicate shared by the card (count and
/// cover) and the shelf's recipe list so the two cannot disagree.
/// </summary>
internal static class SmartShelfSql
{
    /// <summary>Every rule must hold, using the same clauses as the filter bar.</summary>
    /// <param name="tags">The SQL expression holding the required tag slugs.</param>
    /// <param name="ingredients">The SQL expression holding the required ingredients.</param>
    /// <param name="maxMinutes">The SQL expression holding the time ceiling.</param>
    /// <param name="held">How many of <paramref name="ingredients"/> this recipe has, if already aggregated; else asked per recipe.</param>
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
            -- Excludes, unlike the search's ingredient ranking: a recipe without chicken is not on a chicken shelf.
            and (cardinality({ingredients}) = 0
                 or {held ?? Held(ingredients)} = cardinality({ingredients}))
            -- No stated time is excluded by the ceiling, not treated as zero minutes.
            and ({maxMinutes} is null or {RecipeSql.FitsWithin(maxMinutes)})
        """;

    // Per-recipe ingredient count; the card needs it because each shelf has its own rules.
    private static string Held(string ingredients) => $"""
        (select count(*) from unnest({ingredients}) as required
         where exists (
             select 1 from recipe_ingredients ri
             join ingredient_groups g on g.id = ri.group_id
             where g.recipe_id = r.id and ri.name ilike '%' || required || '%'))
        """;
}
