using Application.Abstractions;
using Domain.Search;

namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// Finds the recipes of a household most like one of its own, by the concepts
/// their search documents are indexed under.
/// </summary>
/// <remarks>
/// <para>
/// Two questions, weighted as docs/search-design.md §19.1 weighs them: what a
/// recipe <em>is</em> — a pasta, Italian, baked — counts for 0.6, and what it
/// is <em>made from</em> for 0.4. Somebody looking at a Bolognese is more
/// interested in a Lasagne than in a Chili that happens to share three tins.
/// </para>
/// <para>
/// Every shared concept counts by how rare it is in this kitchen. A document
/// carries every concept with its ancestors, so almost everything is a
/// vegetable dish of some sort, and counting "vegetable" like "mince" would
/// make every recipe related to every other. Weighted by rarity, what nearly
/// every recipe shares counts for nearly nothing, and what two recipes share
/// with few others counts for the most — with no list of concepts to leave
/// out, which would be wrong in a kitchen that cooks nothing but soup.
/// </para>
/// </remarks>
/// <param name="executor">Runs the SQL.</param>
internal sealed class RelatedRecipes(DbExecutor executor) : IRelatedRecipes
{
    /// <summary>
    /// The least a recipe must have in common to be called related at all.
    /// </summary>
    /// <remarks>
    /// Below it the only thing shared is something most of the kitchen shares,
    /// and "also has onions in it" is not a reason anybody wants to be shown.
    /// Measured on the golden library: at 0.15 a salmon recipe lost the fish
    /// tacos and the Frikadellen lost every other mince recipe; at 0.1 every
    /// one of the sixty has at least three, and nothing below it was a
    /// relation anybody would recognise.
    /// </remarks>
    internal const double Floor = 0.1d;

    public async Task<IReadOnlyList<RelatedRecipe>> FindAsync(
        Guid recipeId,
        Guid householdId,
        Guid userId,
        int limit,
        CancellationToken cancellationToken)
    {
        var concepts = await executor.QuerySingleOrDefaultAsync<string[]>(
            "select concepts from recipe_search_documents where recipe_id = @recipeId;",
            new { recipeId },
            cancellationToken).ConfigureAwait(false) ?? [];

        if (concepts.Length == 0)
        {
            return [];
        }

        var stuff = concepts
            .Where(key => CulinaryLexicon.Find(key)?.Kind == ConceptKind.Ingredient)
            .ToArray();

        var rows = await executor.QueryAsync<Row>(
            Sql,
            new { recipeId, householdId, userId, concepts, stuff, limit, floor = Floor },
            cancellationToken).ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new RelatedRecipe(
                new RecipeSearchRow(
                    row.RecipeId,
                    row.Title,
                    row.ImageId,
                    row.TotalMinutes,
                    row.YieldAmount,
                    row.YieldKind,
                    row.YieldLabel,
                    row.Tags,
                    row.CookCount,
                    row.LastCookedAt,
                    row.UpdatedAt,
                    MatchedIngredients: 0,
                    row.IngredientCount,
                    AddedToCookbookAt: null),
                row.Kinds,
                row.Stuff,
                row.KindScore,
                row.StuffScore))
        ];
    }

    /// <remarks>
    /// A concept's weight is its inverse document frequency in the household,
    /// counting the recipe asked about, so that a concept only it and one other
    /// recipe carry is worth the most and one every recipe carries is worth
    /// nothing. Each score is the share of the first recipe's own weight that
    /// the other one matches, so a recipe that is everything the first is
    /// scores 1.
    ///
    /// The shared concepts are listed only where they are telling — carried by
    /// no more than half the kitchen. They still count towards the score, but
    /// "vegetable" is true of half of everything and is not a reason.
    /// </remarks>
    private const string Sql = """
        with library as (
            select count(*)::float8 as size
            from recipe_search_documents
            where household_id = @householdId),
        weighted as (
            select c.concept,
                   c.concept = any(@stuff) as stuff,
                   ln((l.size + 1) / (count(d.recipe_id) + 1)) as weight
            from unnest(@concepts::text[]) as c(concept)
            cross join library l
            left join recipe_search_documents d
                   on d.household_id = @householdId and d.concepts @> array[c.concept]
            group by c.concept, l.size),
        totals as (
            select coalesce(sum(weight) filter (where not stuff), 0) as kinds,
                   coalesce(sum(weight) filter (where stuff), 0) as stuff
            from weighted),
        shared as (
            select d.recipe_id,
                   coalesce(array_agg(w.concept order by w.weight desc, w.concept)
                            filter (where not w.stuff and w.weight >= ln(2)), '{}') as kinds,
                   coalesce(array_agg(w.concept order by w.weight desc, w.concept)
                            filter (where w.stuff and w.weight >= ln(2)), '{}') as stuff,
                   coalesce(sum(w.weight) filter (where not w.stuff), 0) as kind_weight,
                   coalesce(sum(w.weight) filter (where w.stuff), 0) as stuff_weight
            from recipe_search_documents d
            join weighted w on d.concepts @> array[w.concept]
            where d.household_id = @householdId
              and d.recipe_id <> @recipeId
              and w.weight > 0
            group by d.recipe_id),
        scored as (
            select s.*,
                   coalesce(s.kind_weight / nullif(t.kinds, 0), 0) as kind_score,
                   coalesce(s.stuff_weight / nullif(t.stuff, 0), 0) as stuff_score
            from shared s
            cross join totals t)
        select r.id as recipe_id, r.title, r.image_id,
               case when r.prep_minutes is null and r.cook_minutes is null then null
                    else coalesce(r.prep_minutes, 0) + coalesce(r.cook_minutes, 0) end as total_minutes,
               r.yield_amount, r.yield_kind, r.yield_label, r.updated_at,
               coalesce(array(select t.slug from recipe_tags rt
                              join tags t on t.id = rt.tag_id
                              where rt.recipe_id = r.id
                              order by t.slug), '{}') as tags,
               coalesce(mine.cook_count, 0) as cook_count,
               mine.last_cooked_at,
               d.ingredient_count,
               s.kinds, s.stuff, s.kind_score, s.stuff_score
        from scored s
        join recipes r on r.id = s.recipe_id
        join recipe_search_documents d on d.recipe_id = s.recipe_id
        left join lateral (
            select count(*) as cook_count, max(c.made_at) as last_cooked_at
            from cook_log_entries c
            where c.recipe_id = r.id and c.user_id = @userId
        ) mine on true
        where 0.6 * s.kind_score + 0.4 * s.stuff_score >= @floor
        order by 0.6 * s.kind_score + 0.4 * s.stuff_score desc, r.updated_at desc, r.id
        limit @limit;
        """;

    private sealed record Row
    {
        public Guid RecipeId { get; init; }

        public string Title { get; init; } = string.Empty;

        public Guid? ImageId { get; init; }

        public int? TotalMinutes { get; init; }

        public decimal YieldAmount { get; init; }

        public string YieldKind { get; init; } = "servings";

        public string? YieldLabel { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }

        public string[] Tags { get; init; } = [];

        public int CookCount { get; init; }

        public DateTimeOffset? LastCookedAt { get; init; }

        public int IngredientCount { get; init; }

        public string[] Kinds { get; init; } = [];

        public string[] Stuff { get; init; } = [];

        public double KindScore { get; init; }

        public double StuffScore { get; init; }
    }
}
