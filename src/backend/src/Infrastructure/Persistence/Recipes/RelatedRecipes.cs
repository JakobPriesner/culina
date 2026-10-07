using Application.Abstractions;
using Domain.Search;

namespace Infrastructure.Persistence.Recipes;

/// <summary>Finds a household's recipes most like one of its own, by the concepts their search documents are indexed under.</summary>
/// <remarks>
/// Per docs/search-design.md §19.1: what a recipe <em>is</em> counts 0.6 and what it is <em>made from</em> 0.4.
/// Shared concepts are weighted by rarity in this kitchen, so near-universal ones count for almost nothing
/// without a hard-coded exclusion list.
/// </remarks>
internal sealed class RelatedRecipes(DbExecutor executor) : IRelatedRecipes
{
    // The least in common to count as related: below it the only overlap is what most of the kitchen shares.
    // Measured on the golden library; 0.1 gives every one of the sixty at least three results.
    internal const double Floor = 0.1d;

    public async Task<RelatedPage> FindAsync(
        Guid recipeId,
        IReadOnlyList<Guid> library,
        Guid userId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);

        var resume = RelatedCursor.Decode(cursor);

        var concepts = await executor.QuerySingleOrDefaultAsync<string[]>(
            "select concepts from recipe_search_documents where recipe_id = @recipeId;",
            new { recipeId },
            cancellationToken).ConfigureAwait(false) ?? [];

        if (concepts.Length == 0)
        {
            return new RelatedPage([], null);
        }

        var stuff = concepts
            .Where(key => CulinaryLexicon.Find(key)?.Kind == ConceptKind.Ingredient)
            .ToArray();

        // One more than asked for, to know whether there is a next page.
        var rows = (await executor.QueryAsync<Row>(
            Sql.Replace("{resume}", resume is null ? string.Empty : Resume, StringComparison.Ordinal),
            new
            {
                recipeId,
                library = library.ToArray(),
                userId,
                concepts,
                stuff,
                limit = limit + 1,
                floor = Floor,
                score = resume?.Score ?? 0m,
                updatedAt = resume?.UpdatedAt ?? DateTimeOffset.MinValue,
                cursorId = resume?.Id ?? Guid.Empty
            },
            cancellationToken).ConfigureAwait(false)).ToList();

        var page = rows.Take(limit).ToList();
        var next = rows.Count > limit ? new RelatedCursor(page[^1].Score, page[^1].UpdatedAt, page[^1].RecipeId).Encode() : null;

        return new RelatedPage(
        [
            .. page.Select(row => new RelatedRecipe(
                row.ToSearchRow(),
                row.Kinds,
                row.Stuff,
                row.KindScore,
                row.StuffScore))
        ],
            next);
    }

    /// <summary>Starts after the row the previous page ended on, in the same order.</summary>
    private const string Resume = "and (s.score, r.updated_at, r.id) < (@score, @updatedAt, @cursorId)";

    // A concept's weight is its inverse document frequency in the household, counting the recipe asked about.
    // Each score is the share of the first recipe's weight the other matches. Shared concepts are listed only
    // when carried by at most half the kitchen. The score is rounded to six places so a cursor can carry it exactly.
    private static readonly string Sql = $$"""
        with library as (
            select count(*)::float8 as size
            from recipe_search_documents
            where household_id = any(@library)),
        weighted as (
            select c.concept,
                   c.concept = any(@stuff) as stuff,
                   ln((l.size + 1) / (count(d.recipe_id) + 1)) as weight
            from unnest(@concepts::text[]) as c(concept)
            cross join library l
            left join recipe_search_documents d
                   on d.household_id = any(@library) and d.concepts @> array[c.concept]
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
            where d.household_id = any(@library)
              and d.recipe_id <> @recipeId
              and w.weight > 0
            group by d.recipe_id),
        scored as (
            select s.*,
                   coalesce(s.kind_weight / nullif(t.kinds, 0), 0) as kind_score,
                   coalesce(s.stuff_weight / nullif(t.stuff, 0), 0) as stuff_score
            from shared s
            cross join totals t),
        ranked as (
            select s.*,
                   round((0.6 * s.kind_score + 0.4 * s.stuff_score)::numeric, 6) as score
            from scored s)
        select r.id as recipe_id, r.household_id, r.title, r.image_id,
               {{RecipeSql.TotalMinutes()}} as total_minutes,
               r.yield_amount, r.yield_kind, r.yield_label, r.updated_at,
               coalesce(array(select t.slug from recipe_tags rt
                              join tags t on t.id = rt.tag_id
                              where rt.recipe_id = r.id
                              order by t.slug), '{}') as tags,
               coalesce(mine.cook_count, 0) as cook_count,
               mine.last_cooked_at,
               d.ingredient_count,
               s.kinds, s.stuff, s.kind_score, s.stuff_score, s.score
        from ranked s
        join recipes r on r.id = s.recipe_id
        join recipe_search_documents d on d.recipe_id = s.recipe_id
        left join lateral (
            select count(*) as cook_count, max(c.made_at) as last_cooked_at
            from cook_log_entries c
            where c.recipe_id = r.id and c.user_id = @userId
        ) mine on true
        where 0.6 * s.kind_score + 0.4 * s.stuff_score >= @floor
          {resume}
        order by s.score desc, r.updated_at desc, r.id desc
        limit @limit;
        """;

    private sealed record Row : RecipeCardRow
    {
        public string[] Kinds { get; init; } = [];

        public string[] Stuff { get; init; } = [];

        public double KindScore { get; init; }

        public double StuffScore { get; init; }

        public decimal Score { get; init; }
    }
}
