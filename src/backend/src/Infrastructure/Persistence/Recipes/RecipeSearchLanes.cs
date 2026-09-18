namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// What it means for a recipe to match some words, as SQL.
/// </summary>
/// <remarks>
/// <para>
/// Four kinds of evidence, each answering a question the others cannot, and
/// each named so that the ranking can tell them apart. They are written once
/// here because the search asks for them twice — the <c>where</c> decides
/// which recipes are candidates, and the projection records <em>why</em> each
/// one is, which is what the tier in <see cref="Tier"/> is built from. Two
/// hand-kept copies of that would eventually disagree about a recipe, and the
/// symptom would be a result with no visible reason to be there.
/// </para>
/// <para>
/// The division of labour between the lanes is not a matter of taste. Full text
/// owns morphology in both directions — <c>Tomate</c> finds <c>Tomaten</c> and
/// the reverse — because a stemmer reduces both to one lexeme. Trigram owns
/// compounds and typos, because the German Snowball stemmer never decomposes a
/// compound and <c>Hähnchenbrustfilet</c> stays one word forever. Neither
/// subsumes the other, which is why the naive version of this change — swap
/// <c>ilike</c> for <c>@@</c> — would have lost the compounds that already
/// worked.
/// </para>
/// </remarks>
internal static class RecipeSearchLanes
{
    /// <summary>
    /// The query, folded and parsed once, for every row to be compared against.
    /// </summary>
    /// <remarks>
    /// Both text search configurations are built rather than one being chosen,
    /// because a query is too short to say what language it is in — "Pasta",
    /// "Curry" and "Butter" are both at once — while a document says so
    /// outright. The row picks which of the two to use.
    /// </remarks>
    internal const string QueryCte = """
        select
            culina_fold_ae(@query::text)                    as q_ae,
            culina_fold_a(@query::text)                     as q_a,
            culina_search_terms(@query::text)               as terms,
            websearch_to_tsquery('culina_de', @query::text) as tsq_de,
            websearch_to_tsquery('culina_en', @query::text) as tsq_en,
            -- Whether anything searchable survived the fold. A query of
            -- nothing but punctuation — "%", "...", "???" — folds to the empty
            -- string, and an empty string is a prefix of every title. Saying so
            -- here makes "no content is no query" a decision with a name on it
            -- rather than something that falls out of `like '%'` by accident.
            coalesce(culina_fold_ae(@query::text), '') <> '' as has_text
        """;

    /// <summary>
    /// The tsquery this row is asked with, laterally joined so the choice is
    /// made once per row rather than repeated in every expression below.
    /// </summary>
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

    /// <summary>
    /// Whether a lexeme landed in a particular weight band.
    /// </summary>
    /// <remarks>
    /// Ranking the same vector with every weight but one set to zero is the
    /// only way to ask a tsvector <em>where</em> it matched. A tsquery answers
    /// whether, never where, and the tier needs where.
    /// </remarks>
    private static string BandHit(string weights) => $"{Rank(weights)} > 0";

    /// <summary>
    /// The cover-density rank of this row, bounded into [0, 1).
    /// </summary>
    /// <remarks>
    /// Normalisation 32 is <c>rank / (rank + 1)</c>, which is what makes the
    /// value comparable with the other terms of the score. Length
    /// normalisation is deliberately not asked for on top of it: how much of
    /// the title the query accounts for is already a term of its own, and
    /// dividing by the document length here would count that twice.
    /// </remarks>
    private static string Rank(string weights) =>
        $"ts_rank_cd('{{{weights}}}'::float4[], d.document, lang.tsq, 32)";

    /// <summary>
    /// A term inside a word of the title, or near enough to one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The compound lane and the typo lane at once, and the reason
    /// <c>Hähnchen</c> still finds <c>Hähnchenbrustfilet</c> after the stemmer
    /// has given up on it.
    /// </para>
    /// <para>
    /// Similarity is measured against the <em>title</em> and nowhere else, and
    /// that is a claim about people rather than about cost: a misspelling is a
    /// mistyped name. Nobody misspells a word buried in step four and expects
    /// to be understood, and the substring half below already reaches every
    /// word of the recipe. Measured, it is also the difference between three
    /// milliseconds and a hundred and ninety, because a title is thirty
    /// characters and a recipe is twelve hundred.
    /// </para>
    /// </remarks>
    private const string FuzzyTitle = """
        exists (select 1 from unnest(q.terms) as term
                where d.title_ae like '%' || term || '%'
                   or d.title_a like '%' || term || '%'
                   or word_similarity(term, d.title_ae) >= @fuzzyThreshold)
        """;

    /// <summary>
    /// A term inside any word of the recipe.
    /// </summary>
    /// <remarks>
    /// Substring only. This is what answers a compound anywhere — an
    /// ingredient called <c>Süßkartoffel</c> found by typing
    /// <c>Kartoffel</c> — and it stays cheap because a LIKE over a folded
    /// document is a scan the trigram index can help with, where measuring
    /// similarity against the whole document is not.
    /// </remarks>
    private const string FuzzyBody = """
        exists (select 1 from unnest(q.terms) as term
                where d.fuzzy_text like '%' || term || '%')
        """;

    /// <summary>
    /// Which recipes are candidates at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A recipe with no document row is excluded from a text search rather than
    /// from the collection: the join is a left join, so it still lists, still
    /// filters and still pages, and only stops being findable by words until
    /// the next write rebuilds it. A missing document should be impossible —
    /// the migration backfills every row and the writer runs in the recipe's
    /// own transaction — but "impossible" is a poor reason for a recipe to
    /// vanish from a library.
    /// </para>
    /// <para>
    /// Every lane is listed, including the ones the ranking treats as weak.
    /// Narrowing the candidate set is the one thing that cannot be undone
    /// later: a recipe the <c>where</c> rejected is one no amount of ranking
    /// can bring back.
    /// </para>
    /// </remarks>
    internal static string Predicate { get; } = $"""
        q.has_text and d.recipe_id is not null and (
               ({ExactTitle})
            or ({TitleWord})
            or d.document @@ lang.tsq
            or ({FuzzyBody})
            or ({FuzzyTitle}))
        """;

    /// <summary>
    /// Why each candidate is a candidate, recorded per row for the tier.
    /// </summary>
    internal static string Evidence { get; } = $"""
        coalesce({ExactTitle}, false)                    as exact_title,
        coalesce({TitleWord}, false)                     as title_word,
        coalesce({BandHit("0, 0, 0, 1")}, false)         as title_hit,
        coalesce({BandHit("0, 0, 1, 0")}, false)         as tag_hit,
        coalesce(d.document @@ lang.tsq, false)          as lexical_hit,
        coalesce({FuzzyTitle}, false)                    as fuzzy_title,
        coalesce({FuzzyBody}, false)                     as fuzzy_body,
        coalesce({Rank("0.1, 0.3, 0.6, 1.0")}, 0)::float8 as lexical_rank,
        {QueryCoverage}                                  as query_coverage,
        {TitleCoverage}                                  as title_coverage
        """;

    /// <summary>
    /// How much of what was asked for this recipe accounts for.
    /// </summary>
    /// <remarks>
    /// A query with no terms of its own — one short word, or nothing but
    /// joining words — has nothing to cover, and scores one rather than zero.
    /// The value is the same for every candidate either way, so it changes no
    /// order; it keeps scores comparable between queries, which matters when
    /// somebody is reading them in a test failure.
    /// </remarks>
    private const string QueryCoverage = """
        case
            when coalesce(cardinality(q.terms), 0) = 0 then 1.0::float8
            else (select count(*) from unnest(q.terms) as term
                  where d.fuzzy_text like '%' || term || '%')::float8
                 / cardinality(q.terms)
        end
        """;

    /// <summary>
    /// How much of the title the query accounts for: the specificity signal.
    /// </summary>
    /// <remarks>
    /// "Bolognese" is all of <em>Bolognese</em>, half of <em>Spaghetti
    /// Bolognese</em> and a third of <em>Lasagne Bolognese Auflauf</em>, so the
    /// recipe that most nearly <em>is</em> the query comes first. This is field
    /// length normalisation — the same idea as BM25's <c>b</c> — expressed
    /// where it can be read.
    /// </remarks>
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

    /// <summary>
    /// How confident the system is about <em>why</em> a recipe is here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tier is compared before the score, so evidence of a better kind
    /// always outranks more of a worse kind. That is what keeps a recipe that
    /// merely resembles the query below one that is named by it, however the
    /// weights below are tuned — a guarantee a single blended number cannot
    /// make, because there is always some combination of signals that adds up
    /// to more.
    /// </para>
    /// <para>
    /// A title compound (tier 2) sits above an exact ingredient (tier 3) on
    /// purpose. Somebody typing <c>Hähnchen</c> means the chicken dish before
    /// the stew that happens to contain some.
    /// </para>
    /// </remarks>
    internal const string Tier = """
        case
            when not has_text                  then 0
            when exact_title                   then 0
            when title_word or title_hit       then 1
            when fuzzy_title or tag_hit        then 2
            when lexical_hit                   then 3
            else                                    4
        end
        """;

    /// <summary>
    /// How well a candidate fits, within its tier.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four continuous signals, weighted by hand against the cases in
    /// <c>RecipeSearchTests</c> and summing to one. Deliberately not five: an
    /// earlier draft also scored <em>which field</em> matched and <em>how</em>,
    /// which the tier already decides, and counting the same evidence in both
    /// places makes the tuning of one silently undo the other.
    /// </para>
    /// <para>
    /// Every term is bounded in [0, 1] and computed per row, so nothing is
    /// normalised across the result set. Saving a recipe therefore cannot
    /// reorder the ones around it — in a library somebody adds to every few
    /// days, a ranking that visibly reshuffles on every write is a ranking
    /// people stop trusting.
    /// </para>
    /// </remarks>
    internal const string Score = """
          0.35 * title_coverage
        + 0.30 * query_coverage
        + 0.20 * lexical_rank
        + 0.15 * structural_fit
        """;

    /// <summary>
    /// How well a recipe fits the ingredients somebody said they had.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses as many as possible of what was named, and needs as few extras as
    /// possible — the ordering Culina's "what can I cook?" already had, kept
    /// here as one term of the score rather than as a sort of its own. Zero
    /// when no ingredients were named, which is the same for every candidate
    /// and so changes nothing.
    /// </para>
    /// <para>
    /// The penalty for extras is capped at half of one match, so it can only
    /// ever break a tie between recipes that use the same number of the named
    /// ingredients. Using one more of what somebody actually has always beats
    /// needing fewer things they do not — which is what the two-key ordering
    /// this replaces meant, preserved exactly rather than approximately.
    /// </para>
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
