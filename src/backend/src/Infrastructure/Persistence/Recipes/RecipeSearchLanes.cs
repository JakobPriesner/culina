namespace Infrastructure.Persistence.Recipes;

/// <summary>What it means for a recipe to match some words, as SQL.</summary>
/// <remarks>
/// <see cref="Hits"/> picks candidates and <see cref="Evidence"/> records why; the two must agree.
/// Full text owns morphology, trigram owns compounds and typos (the German stemmer never decomposes
/// a compound).
/// </remarks>
internal static class RecipeSearchLanes
{
    /// <summary>
    /// Whether anything searchable survived the fold; a punctuation-only query folds to empty,
    /// which prefixes every title.
    /// </summary>
    internal const string HasText = "coalesce(culina_fold_ae(@query::text), '') <> ''";

    /// <summary>The query, folded and parsed once, for every row to be compared against.</summary>
    /// <remarks>
    /// Both text search configurations are built: a query is too short to say its language ("Pasta"
    /// is both), so the row picks.
    /// </remarks>
    internal const string QueryCte = $"""
        select
            culina_fold_ae(@query::text)                    as q_ae,
            culina_fold_a(@query::text)                     as q_a,
            culina_search_terms(@query::text)               as terms,
            websearch_to_tsquery('culina_de', @query::text) as tsq_de,
            websearch_to_tsquery('culina_en', @query::text) as tsq_en,
            {HasText} as has_text
        """;

    /// <summary>The tsquery this row is asked with, chosen once per row.</summary>
    internal const string LanguageJoin = """
        cross join lateral (
            select case when d.language = 'de' then q.tsq_de else q.tsq_en end as tsq
        ) lang
        """;

    /// <summary>The title, as typed, in either spelling.</summary>
    private const string ExactTitle = "d.title_ae = q.q_ae or d.title_a = q.q_a";

    /// <summary>The title begins with the query, or contains it as a whole word.</summary>
    private const string TitleWord = """
        d.title_ae like q.q_ae || '%' or d.title_a like q.q_a || '%'
        or (' ' || d.title_ae || ' ') like ('% ' || q.q_ae || ' %')
        or (' ' || d.title_a || ' ') like ('% ' || q.q_a || ' %')
        """;

    /// <summary>Whether the query matched inside one weight band of the document.</summary>
    /// <remarks>
    /// A tsquery says whether, never where, so the vector is cut to one band and asked again, far
    /// cheaper than a rank per band. A query with a minus only matches in a band when the document
    /// matches at all.
    /// </remarks>
    private static string BandHit(char weight) =>
        $"(d.document @@ lang.tsq and querytree(lang.tsq) <> 'T' "
        + $"and ts_filter(d.document, '{{{weight}}}') @@ lang.tsq)";

    /// <summary>Cover-density rank, normalised into [0, 1).</summary>
    /// <remarks>
    /// Zero without asking when the document does not match: <c>ts_rank_cd</c> would still scan it,
    /// and most candidates are substring-lane compounds.
    /// </remarks>
    private const string Rank = """
        case when d.document @@ lang.tsq
             then ts_rank_cd('{0.1, 0.3, 0.6, 1.0}'::float4[], d.document, lang.tsq, 32)
             else 0 end
        """;

    /// <summary>
    /// A term inside a word of the title, or near enough to one: the compound and typo lane.
    /// </summary>
    /// <remarks>
    /// Measured against the title only: a misspelling is a mistyped name, and against a whole
    /// document it is ~60x slower.
    /// </remarks>
    private const string FuzzyTitle = """
        exists (select 1 from unnest(q.terms) as term
                where d.title_ae like '%' || term || '%'
                   or d.title_a like '%' || term || '%'
                   or strict_word_similarity(term, d.title_ae) >= @fuzzyThreshold)
        """;

    /// <summary>
    /// A term inside any word of the recipe; substring only, which the trigram index can help with.
    /// </summary>
    private const string FuzzyBody = """
        exists (select 1 from unnest(q.terms) as term
                where d.fuzzy_text like '%' || term || '%')
        """;

    /// <summary>
    /// Whether a recipe answers every concept the query names, each by itself or by what may stand
    /// in for it.
    /// </summary>
    /// <remarks>
    /// <c>@conceptAnswers</c> lists what may answer each concept, <c>@conceptAsked</c> which of
    /// <c>@concepts</c> each answers; see <see cref="Domain.Search.CulinaryLexicon.AnsweredBy"/>.
    /// </remarks>
    private const string ConceptHit = """
        (select count(distinct answer.asked)
         from unnest(@conceptAnswers::text[], @conceptAsked::int[]) as answer(concept, asked)
         where answer.concept = any(d.concepts)) = cardinality(@concepts::text[])
        """;

    /// <summary>
    /// Whether the recipe carries a tag a query word is built on; see
    /// <see cref="Domain.Search.SearchText.Modifiers"/>.
    /// </summary>
    private const string TagNamed = """
        cardinality(@tagWords::text[]) > 0 and exists (
            select 1 from recipe_tags rt
            join tags t on t.id = rt.tag_id
            where rt.recipe_id = r.id and culina_fold_ae(t.name) = any(@tagWords::text[]))
        """;

    /// <summary>Which recipes are candidates at all: one index scan per lane, unioned.</summary>
    /// <remarks>
    /// A union, because lanes joined by one <c>or</c> are a condition no index serves. The query is
    /// repeated in every branch rather than read from <c>q</c>: only a parameter is known at plan
    /// time, which turns the title prefix scan into a btree range.
    /// </remarks>
    internal const string Hits = $"""
        -- Lexical, by the document's own language: what the stemmer can see.
        select d.recipe_id from recipe_search_documents d
        where d.household_id = any(@library) and d.language = 'de'
          and d.document @@ websearch_to_tsquery('culina_de', @query::text)
        union
        select d.recipe_id from recipe_search_documents d
        where d.household_id = any(@library) and d.language <> 'de'
          and d.document @@ websearch_to_tsquery('culina_en', @query::text)
        union
        -- A term inside any word of the recipe: compounds, everywhere.
        select d.recipe_id
        from unnest(culina_search_terms(@query::text)) as term
        join recipe_search_documents d on d.fuzzy_text like '%' || term || '%'
        where d.household_id = any(@library)
        union
        -- A term near enough to a word of the title: typos (see migration 0018).
        select d.recipe_id
        from unnest(culina_search_terms(@query::text)) as term
        join recipe_search_documents d on term <<% d.title_ae
        where d.household_id = any(@library)
          and strict_word_similarity(term, d.title_ae) >= @fuzzyThreshold
        union
        -- Every concept the query names, among those the title, tags and ingredients carry (every,
        -- not any). The overlap is the index's way in; the count is the rule.
        select d.recipe_id from recipe_search_documents d
        where d.household_id = any(@library)
          and cardinality(@concepts::text[]) > 0
          and d.concepts && @conceptAnswers::text[]
          and {ConceptHit}
        union
        -- A household tag a query word is built on ("Sommergericht" for "Sommer"): its own word
        -- beats the lexicon's inference.
        select rt.recipe_id from tags t
        join recipe_tags rt on rt.tag_id = t.id
        where t.household_id = any(@library)
          and culina_fold_ae(t.name) = any(@tagWords::text[])
        union
        -- The title, for a query too short to have a term of its own.
        select d.recipe_id from q
        cross join recipe_search_documents d
        where cardinality(culina_search_terms(@query::text)) = 0
          and d.household_id = any(@library)
          and ({TitleWord})
        """;

    /// <summary>Why each candidate is a candidate, recorded per row for the tier.</summary>
    internal static string Evidence { get; } = $"""
        coalesce({ExactTitle}, false)                    as exact_title,
        coalesce({TitleWord}, false)                     as title_word,
        coalesce({BandHit('a')}, false)                  as title_hit,
        coalesce({BandHit('b')}, false)                  as tag_hit,
        coalesce(d.document @@ lang.tsq, false)          as lexical_hit,
        coalesce({FuzzyTitle}, false)                    as fuzzy_title,
        coalesce({FuzzyBody}, false)                     as fuzzy_body,
        coalesce({TagNamed}, false)                      as tag_named,
        coalesce(cardinality(@concepts::text[]) > 0 and {ConceptHit}, false) as concept_hit,
        coalesce(cardinality(@diets::text[]) > 0 and d.concepts @> @diets::text[], false) as diet_asserted,
        coalesce(@quick and d.concepts @> array['quick'], false) as quick_asserted,
        {MealFit}                                        as meal_fit,
        {TitleSimilarity}                                as title_similarity,
        coalesce({Rank}, 0)::float8                      as lexical_rank,
        {QueryCoverage}                                  as query_coverage,
        {TitleCoverage}                                  as title_coverage
        """;

    /// <summary>
    /// How much of what was asked for this recipe accounts for; one when nothing was asked, so
    /// scores stay comparable.
    /// </summary>
    private const string QueryCoverage = """
        case
            when coalesce(cardinality(q.terms), 0) = 0 then 1.0::float8
            else (select count(*) from unnest(q.terms) as term
                  where d.fuzzy_text like '%' || term || '%')::float8
                 / cardinality(q.terms)
        end
        """;

    /// <summary>
    /// How much of the title the query accounts for, so "Bolognese" beats "Lasagne Bolognese
    /// Auflauf" (field-length normalisation).
    /// </summary>
    private const string TitleCoverage = """
        case
            when d.title_ae is null or d.title_ae = '' or coalesce(cardinality(q.terms), 0) = 0
                then 0.0::float8
            else (select count(*)
                  from unnest(string_to_array(d.title_ae, ' ')) as word
                  where exists (select 1 from unnest(q.terms) as term
                                where word like '%' || term || '%'))::float8
                 / greatest(array_length(string_to_array(d.title_ae, ' '), 1), 1)
        end
        """;

    /// <summary>How confident the system is about why a recipe is here.</summary>
    /// <remarks>
    /// Compared before the score, so better evidence always outranks more of a worse kind, however
    /// the weights are tuned. The lexicon alone is last, below a household tag: a substring is a
    /// fact about the recipe, the lexicon a guess.
    /// </remarks>
    internal const string Tier = """
        case
            when not has_text                  then 0
            when exact_title                   then 0
            when title_word or title_hit       then 1
            when fuzzy_title or tag_hit        then 2
            when lexical_hit                   then 3
            when fuzzy_body or tag_named       then 4
            else                                    5
        end
        """;

    /// <summary>How well a candidate fits, within its tier.</summary>
    /// <remarks>
    /// Four weighted signals summing to one, plus nudges that are zero unless asked for.
    /// Every term is per row in [0, 1], so saving a recipe never reorders its neighbours.
    /// </remarks>
    internal const string Score = """
          0.35 * title_coverage
        + 0.30 * query_coverage
        + 0.20 * lexical_rank
        + 0.15 * structural_fit
        + 0.10 * quick_fit
        + 0.10 * title_similarity
        + 0.10 * case when diet_asserted then 1.0 else 0.0 end
        + 0.10 * case when quick_asserted then 1.0 else 0.0 end
        + 0.10 * meal_fit
        """;

    /// <summary>
    /// How nearly a term of the query is a whole word of the title: orders the typo tier, and only
    /// breaks ties beyond the four weights.
    /// </summary>
    private const string TitleSimilarity = """
        coalesce((select max(strict_word_similarity(term, d.title_ae)) from unnest(q.terms) as term), 0)::float8
        """;

    /// <summary>How well a recipe answers "schnell", when asked.</summary>
    /// <remarks>
    /// A nudge, never a filter: known-quick recipes are lifted, ones with no stated time half as
    /// far, nothing is removed.
    /// </remarks>
    internal const string QuickFit = """
        case
            when not @quick then 0.0::float8
            when total_minutes is null then 0.5::float8
            when total_minutes <= 30 then 1.0::float8
            when total_minutes <= 45 then 0.5::float8
            else 0.0::float8
        end
        """;

    /// <summary>
    /// How well a recipe suits a meal no recipe said it was; zero unless one was set aside. See
    /// <see cref="Domain.Search.MealRules"/>.
    /// </summary>
    private const string MealFit = """
        case
            when cardinality(@mealsLike::text[]) = 0 then 0.0::float8
            when coalesce(d.concepts && @mealsUnlike::text[], false) then 0.0::float8
            when coalesce(d.concepts && @mealsLike::text[], false) then 1.0::float8
            else 0.5::float8
        end
        """;

    /// <summary>How well a recipe fits the ingredients somebody said they had.</summary>
    /// <remarks>
    /// Rewards named ingredients used, penalises extras by at most half a match, so extras only
    /// break ties.
    /// </remarks>
    internal const string StructuralFit = """
        case
            when @ingredientCount = 0 then 0.0::float8
            else greatest(
                (matched_ingredients::float8
                    - least(ingredient_count - matched_ingredients, 20) / 40.0)
                / @ingredientCount,
                0.0::float8)
        end
        """;
}
