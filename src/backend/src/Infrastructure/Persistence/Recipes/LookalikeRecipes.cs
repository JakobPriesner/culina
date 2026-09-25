using Application.Abstractions;

namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// Finds the recipe a household already has that a new one looks like, by the
/// three tests of docs/search-design.md §19.2.
/// </summary>
/// <remarks>
/// <para>
/// In order of certainty: the same title once folded — "Käsekuchen" and
/// "Kaesekuchen" are one name; a title that is almost the same and most of the
/// same ingredients — "Omas Spaghetti Bolognese"; or a recipe the lexicon
/// reads as exactly the same thing, made from nearly all the same ingredients —
/// "Linsen Suppe" and "Linsensuppe".
/// </para>
/// <para>
/// A wrong warning is worse than a missed one, so every test that does not
/// rest on the name also asks for <see cref="FewestShared"/> ingredients in
/// common. Two three-ingredient recipes can share everything and still be
/// different dishes; four shared ingredients and a name to match are a
/// Bolognese imported twice.
/// </para>
/// <para>
/// Ingredients are compared by their folded names, over the larger of the two
/// lists, so a recipe with twelve ingredients is not the duplicate of one with
/// three of them.
/// </para>
/// </remarks>
/// <param name="executor">Runs the SQL.</param>
internal sealed class LookalikeRecipes(DbExecutor executor) : ILookalikeRecipes
{
    /// <summary>How alike two titles must be for "almost the same name".</summary>
    internal const double SimilarTitle = 0.85d;

    /// <summary>How much of the ingredients an almost-same name must share.</summary>
    internal const double SimilarTitleOverlap = 0.7d;

    /// <summary>How much of the ingredients the same kind of dish must share.</summary>
    internal const double SameKindOverlap = 0.8d;

    /// <summary>The fewest shared ingredients that can say two differently named recipes are one.</summary>
    internal const int FewestShared = 3;

    public async Task<Lookalike?> FindAsync(
        Guid householdId,
        Guid userId,
        LookalikeCandidate candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return await executor.QuerySingleOrDefaultAsync<Lookalike>(
            Sql,
            new
            {
                householdId,
                userId,
                title = candidate.Title,
                names = candidate.Ingredients.ToArray(),
                concepts = candidate.Concepts.ToArray(),
                similarTitle = SimilarTitle,
                similarTitleOverlap = SimilarTitleOverlap,
                sameKindOverlap = SameKindOverlap,
                fewestShared = FewestShared
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <remarks>
    /// Every recipe of the household is compared, which is one pass over a
    /// title column of a few thousand short strings — measured at a couple of
    /// milliseconds for two thousand — and happens once per recipe imported.
    /// Only the few that pass a title or concept test have their ingredients
    /// counted.
    /// </remarks>
    private const string Sql = """
        with incoming as (
            select distinct culina_fold_ae(name) as name
            from unnest(@names::text[]) as name
            where btrim(name) <> ''),
        typed as (
            select culina_fold_ae(@title) as title_ae,
                   (select count(*) from incoming) as size),
        candidates as (
            select d.recipe_id,
                   d.title_ae = t.title_ae as same_title,
                   greatest(word_similarity(t.title_ae, d.title_ae),
                            word_similarity(d.title_ae, t.title_ae)) as title_similarity,
                   cardinality(@concepts::text[]) > 0 and d.concepts = @concepts::text[] as same_kind
            from recipe_search_documents d
            cross join typed t
            where d.household_id = @householdId),
        compared as (
            select c.*,
                   ingredients.shared,
                   ingredients.shared::float8 / greatest(ingredients.theirs, t.size, 1) as overlap
            from candidates c
            cross join typed t
            cross join lateral (
                select count(distinct i.name_ae) filter (where i.name_ae in (select name from incoming)) as shared,
                       count(distinct i.name_ae) as theirs
                from recipe_ingredients i
                join ingredient_groups g on g.id = i.group_id
                where g.recipe_id = c.recipe_id) ingredients
            where c.same_title or c.title_similarity >= @similarTitle or c.same_kind)
        select r.id as recipe_id, r.title, c.shared::int as shared_ingredients,
               (select count(*)::int from cook_log_entries l
                where l.recipe_id = r.id and l.user_id = @userId) as cook_count
        from compared c
        join recipes r on r.id = c.recipe_id
        where c.same_title
           or (c.title_similarity >= @similarTitle and c.overlap >= @similarTitleOverlap
               and c.shared >= @fewestShared)
           or (c.same_kind and c.overlap >= @sameKindOverlap and c.shared >= @fewestShared)
        order by c.same_title desc, c.overlap desc, r.updated_at desc, r.id
        limit 1;
        """;
}
