using System.Text.RegularExpressions;
using Application.Abstractions;
using Dapper;
using Domain.Search;
using Domain.Suggestions;
using Infrastructure.Persistence.Cookbooks;
using Infrastructure.Persistence.Suggestions;

namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// Finds recipes in one query, so the count and the page cannot disagree.
/// </summary>
/// <param name="executor">Runs the SQL.</param>
/// <param name="time">The clock the suggested order is ranked against.</param>
/// <param name="weights">What each term of the suggested order is worth.</param>
internal sealed partial class RecipeSearcher(DbExecutor executor, TimeProvider time, RankingWeights weights)
{
    private readonly RecipeSearchParameters arguments = new(time, weights);

    /// <summary>A hard ceiling, enforced here and not only in the endpoint.</summary>
    internal const int MaxLimit = 100;

    /// <summary>How alike two words have to be before one counts as the other misspelt.</summary>
    /// <remarks>
    /// Strict word similarity against a whole title word: the looser measure matched "Schnitzel" to "Schnittlauch".
    /// A relevance parameter, so a constant. Also the connection's <c>pg_trgm.strict_word_similarity_threshold</c> (see <see cref="CulinaDataSource"/>).
    /// </remarks>
    internal const double FuzzyThreshold = 0.5d;

    // Built once per shape rather than per request.
    private static readonly string ScoredCandidates = Candidates(scored: true);

    private static readonly string PlainCandidates = Candidates(scored: false);

    private static readonly string ScoredFilter = Filtered(scored: true);

    private static readonly string PlainFilter = Filtered(scored: false);

    // Which recipes hold each named ingredient, computed once per query. A correlated subquery
    // cannot use the trigram index and was ~12x slower; counted by position, as that subquery was.
    private static string Holders(string ingredients) => $"""
        select g.recipe_id, count(distinct one.position) as matched
        from unnest({ingredients}) with ordinality as one(name, position)
        join recipe_ingredients ri on ri.name ilike '%' || one.name || '%'
        join ingredient_groups g on g.id = ri.group_id
        join recipes owner on owner.id = g.recipe_id
                          and owner.household_id = any(@library)
        group by g.recipe_id
        """;

    // The tables a search reads; apart from Rules so the count skips the cook log.
    private static string Sources(bool scored) => $"""
        from q
        cross join recipes r
        left join recipe_search_documents d on d.recipe_id = r.id
        left join wanted on wanted.recipe_id = r.id
        left join required on required.recipe_id = r.id
        left join unwanted on unwanted.recipe_id = r.id
        {(scored ? "left join suggestion_scores s on s.recipe_id = r.id" : string.Empty)}
        {RecipeSearchLanes.LanguageJoin}
        """;

    // Which rows survive; shared by the count and the candidates so a total cannot disagree with its list.
    private static string Rules(bool scored) => $$"""
        where r.household_id = any(@library)
          -- Hidden from the suggested order only: "stop suggesting this" is not "delete this".
          {{(scored ? "and not coalesce(s.dismissed, false)" : string.Empty)}}
          -- Words are answered by `hits`; both escapes are planner-time, so `hits` never runs without words.
          and (@query::text is null or not ({{RecipeSearchLanes.HasText}})
               or r.id in (select recipe_id from hits))
          and (@tagCount = 0 or (
                select count(distinct t.slug) from recipe_tags rt
                join tags t on t.id = rt.tag_id
                where rt.recipe_id = r.id and t.slug = any(@tags::text[])) = @tagCount)
          -- A shelf is a filter over the library, so every other filter works inside a cookbook.
          and (@cookbookId is null or exists (
                select 1 from cookbook_recipes cr
                where cr.cookbook_id = @cookbookId and cr.recipe_id = r.id))
          -- Smart-shelf rules live in SmartShelfSql; the cookbook card counts with the same predicate.
          and {{SmartShelfSql.Matches(
                    "@ruleTags::text[]",
                    "@ruleIngredients::text[]",
                    "@ruleMaxMinutes",
                    held: "coalesce(required.matched, 0)")}}
          -- Diet: kept when the title or a tag says so, or, for refutable diets, when no ingredient
          -- refutes it. A missing document shows neither, so it presumes nothing.
          and (cardinality(@diets::text[]) = 0 or coalesce(
                d.concepts @> @diets::text[]
                or (@dietPresumable and not (d.concepts && @dietRefutedBy::text[])), false))
          and (cardinality(@meals::text[]) = 0 or coalesce(d.concepts && @meals::text[], false))
          and (cardinality(@cuisines::text[]) = 0 or coalesce(d.concepts && @cuisines::text[], false))
          and (cardinality(@ingredientConcepts::text[]) = 0
               or coalesce(d.concepts && @ingredientConcepts::text[], false))
          -- Excluded by lexicon concept, or by ingredient name when the lexicon does not know it.
          and (cardinality(@excludedConcepts::text[]) = 0
               or not coalesce(d.concepts && @excludedConcepts::text[], false))
          and unwanted.recipe_id is null
          -- A recipe with no stated time is excluded, not counted as zero minutes.
          and (@maxMinutes is null or {{RecipeSql.FitsWithin("@maxMinutes")}})
        """;

    // A candidate row: identity plus what the ordering needs. The tag list is deliberately
    // absent (correlated and costly for a whole library); only the scored order pays for the scoring join.
    private static string Candidates(bool scored) => $$"""
        select
            r.id,
            r.household_id,
            r.title,
            r.image_id,
            {{RecipeSql.TotalMinutes()}} as total_minutes,
            r.yield_amount,
            r.yield_kind,
            r.yield_label,
            r.language,
            r.updated_at,
            -- One lookup for count and latest entry; here, not below the page, because the most-cooked order sorts by it.
            coalesce(mine.cook_count, 0) as cook_count,
            mine.last_cooked_at,
            {{(scored ? "coalesce(s.score, 0)" : "0::numeric")}} as suggestion_score,
            -- From the document; the subquery only covers a missing document (it cost 620 ms per 2000 recipes).
            coalesce(d.ingredient_count, (
                select count(*) from recipe_ingredients ri
                join ingredient_groups g on g.id = ri.group_id
                where g.recipe_id = r.id)) as ingredient_count,
            coalesce(wanted.matched, 0) as matched_ingredients,
            (select cr.added_at from cookbook_recipes cr
             where cr.cookbook_id = @cookbookId and cr.recipe_id = r.id) as added_to_cookbook_at,
            q.has_text,
            {{RecipeSearchLanes.Evidence}}
        {{Sources(scored)}}
        left join lateral (
            select count(*) as cook_count, max(c.made_at) as last_cooked_at
            from cook_log_entries c
            where c.recipe_id = r.id and c.user_id = @userId
        ) mine on true
        {{Rules(scored)}}
        """;

    internal async Task<RecipePage> SearchAsync(
        RecipeSearch search,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var limit = Math.Clamp(search.Limit, 1, MaxLimit);
        var cursor = RecipeCursor.Decode(search.Cursor, search.Sort);
        var resume = cursor is null ? null : RecipeSearchSql.ResumePredicate(search.Sort);
        var scored = search.Sort == RecipeSort.Suggested;

        // Joined in only when the order asks for it.
        var scoring = scored ? $"{SuggestionScoringSql.Ctes},\n            " : string.Empty;

        var order = RecipeSearchSql.OrderBy(search.Sort);

        // One extra row says whether a next page exists. The total counts `matching` (ids only):
        // counting the full projection would stop the limit reaching an index.
        var sql = $$"""
            with {{scoring}}{{(scored ? ScoredFilter : PlainFilter)}},
            candidates as ({{(scored ? ScoredCandidates : PlainCandidates)}}),
            ranked as (
                select *,
                    ingredient_count - matched_ingredients as extra_ingredients,
                    {{RecipeSearchLanes.Tier}} as tier,
                    {{RecipeSearchLanes.StructuralFit}} as structural_fit,
                    {{RecipeSearchLanes.QuickFit}} as quick_fit
                from candidates),
            scored as (select *, {{RecipeSearchLanes.Score}} as score from ranked),
            page as (
                select * from scored
                {{(resume is null ? string.Empty : $"where {resume}")}}
                order by {{order}}
                limit {{limit + 1}})
            select
                page.*,
                (select count(*) from matching) as total_count,
                -- Looked up for the page's rows only, not the library.
                coalesce(
                    array(
                        select t.slug from recipe_tags rt
                        join tags t on t.id = rt.tag_id
                        where rt.recipe_id = page.id
                        order by t.slug),
                    '{}') as tags,
                reason.kind as reason_kind,
                reason.term as reason_term
            from page
            cross join q
            left join lateral ({{MatchReasonSql}}) reason on true
            order by {{order}};
            """;

        var rows = await executor.QueryAsync<RecipeSearchRowData>(
            sql,
            arguments.Build(search, cursor, scored),
            cancellationToken).ConfigureAwait(false);

        var page = rows.Take(limit).ToList();

        // A diet is only presumed when every requested diet can be; unasserted rows carry none.
        var presumable = search.Constraints.Diets is [var first, ..] ? first : null;

        return new RecipePage(
            [.. page.Select(data => ToRow(data) with { PresumedDiet = data.DietAsserted ? null : presumable })],
            NextCursorFor(search.Sort, rows.Count > limit, page),
            rows.Count == 0 ? 0 : rows[0].TotalCount);
    }

    /// <summary>Counts what every match could be narrowed by, over the same <c>matching</c> set as the page.</summary>
    internal async Task<SearchFacets> FacetsAsync(RecipeSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var sql = $$"""
            with {{PlainFilter}}
            select 'tag' as kind, t.slug as value, min(t.name) as label, count(*)::int as count
            from matching m
            join recipe_tags rt on rt.recipe_id = m.id
            join tags t on t.id = rt.tag_id
            group by t.slug
            union all
            select 'time', band::text, null, count(*)::int
            from matching m
            join recipes r on r.id = m.id
            cross join unnest(array[15, 30, 45, 60]) as band
            where {{RecipeSql.FitsWithin("band")}}
            group by band
            union all
            select 'cuisine', cuisine, null, count(*)::int
            from matching m
            join recipe_search_documents d on d.recipe_id = m.id
            cross join unnest(d.concepts) as cuisine
            where cuisine = any(@cuisineKeys::text[])
            group by cuisine
            union all
            select 'total', '', null, (select count(*)::int from matching);
            """;

        var parameters = arguments.Build(search, cursor: null, scored: false);
        parameters.Add("cuisineKeys", Cuisines);

        var rows = await executor.QueryAsync<FacetRow>(sql, parameters, cancellationToken).ConfigureAwait(false);

        List<Facet> Of(string kind) =>
            [.. rows.Where(row => row.Kind == kind).Select(row => new Facet(row.Value, row.Label, row.Count))];

        return new SearchFacets(
            rows.Where(row => row.Kind == "total").Select(row => row.Count).FirstOrDefault(),
            Of("tag"),
            Of("time"),
            Of("cuisine"));
    }

    private static readonly string[] Cuisines =
        [.. CulinaryLexicon.All.Where(concept => concept.Kind == ConceptKind.Cuisine).Select(concept => concept.Key)];

    private sealed record FacetRow
    {
        public string Kind { get; init; } = string.Empty;

        public string Value { get; init; } = string.Empty;

        public string? Label { get; init; }

        public int Count { get; init; }
    }

    // Every table expression that decides which recipes match; the page, count and facets are cut from it.
    private static string Filtered(bool scored) => $$"""
        q as ({{RecipeSearchLanes.QueryCte}}),
        hits as ({{RecipeSearchLanes.Hits}}),
        wanted as ({{Holders("@ingredients::text[]")}}),
        required as ({{Holders("@ruleIngredients::text[]")}}),
        unwanted as ({{Holders("@excludedTerms::text[]")}}),
        matching as (select r.id {{Sources(scored)}} {{Rules(scored)}})
        """;

    // Why a page row answers the query when its title does not. Nothing for a title match;
    // always something for a lexicon-only match, so an associative hit looks different.
    // Run on the page's rows only, after ordering.
    private const string MatchReasonSql = """
        select
            case
                when not page.has_text or page.tier <= 1 or page.fuzzy_title then null
                when page.tier = 5 then 'concept'
                when found_ingredient.name is not null then 'ingredient'
                when found_tag.name is not null then 'tag'
                else 'text'
            end as kind,
            case
                when not page.has_text or page.tier <= 1 or page.fuzzy_title then null
                -- For a stand-in this is the recipe's concept, not the asked one: Beef Stew answers "Gulasch" as a stew.
                when page.tier = 5 then (
                    select answer.concept
                    from unnest(@conceptAnswers::text[]) with ordinality as answer(concept, at)
                    join recipe_search_documents d on d.recipe_id = page.id
                    where answer.concept = any(d.concepts)
                    order by answer.at
                    limit 1)
                else coalesce(found_ingredient.name, found_tag.name)
            end as term
        from (select 1) as one
        left join lateral (
            select i.name from recipe_ingredients i
            join ingredient_groups g on g.id = i.group_id
            where g.recipe_id = page.id
              and exists (select 1 from unnest(q.terms) as term
                          where culina_fold_ae(i.name) like '%' || term || '%'
                             or culina_fold_a(i.name) like '%' || term || '%')
            order by g.sort_order, i.sort_order
            limit 1) found_ingredient on true
        left join lateral (
            select t.name from recipe_tags rt
            join tags t on t.id = rt.tag_id
            where rt.recipe_id = page.id
              and (exists (select 1 from unnest(q.terms) as term
                           where culina_fold_ae(t.name) like '%' || term || '%'
                              or culina_fold_a(t.name) like '%' || term || '%')
                   or culina_fold_ae(t.name) = any(@tagWords::text[]))
            order by t.slug
            limit 1) found_tag on true
        """;

    private static string? NextCursorFor(
        RecipeSort sort,
        bool hasMore,
        List<RecipeSearchRowData> page) =>
        hasMore && page.Count > 0
            ? new RecipeCursor(sort, RecipeSearchSql.KeysOf(sort, page[^1]), page[^1].Id).Encode()
            : null;

    private static RecipeSearchRow ToRow(RecipeSearchRowData data) => data.ToSearchRow() with
    {
        Reason = data.ReasonKind is { } kind ? new MatchReason(kind, data.ReasonTerm, data.Language) : null
    };
}
