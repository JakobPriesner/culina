namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// What it means for a recipe to match some words, as SQL.
/// </summary>
/// <remarks>
/// <para>
/// Four kinds of evidence, each answering a question the others cannot, and
/// each named so that the ranking can tell them apart. The search asks about
/// them twice — <see cref="Hits"/> decides which recipes are candidates, and
/// <see cref="Evidence"/> records <em>why</em> each one is, which is what the
/// tier in <see cref="Tier"/> is built from — and both are here, next to each
/// other, because they have to agree. They cannot share one text: the first
/// has to be shaped for an index and the second for a single row. If they
/// ever drift, the symptom is a result with no visible reason to be there,
/// which the tier puts last rather than hiding.
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
    /// Whether anything searchable survived the fold.
    /// </summary>
    /// <remarks>
    /// A query of nothing but punctuation — "%", "...", "???" — folds to the
    /// empty string, and an empty string is a prefix of every title. Saying so
    /// once makes "no content is no query" a decision with a name on it rather
    /// than something that falls out of <c>like '%'</c> by accident.
    /// </remarks>
    internal const string HasText = "coalesce(culina_fold_ae(@query::text), '') <> ''";

    /// <summary>
    /// The query, folded and parsed once, for every row to be compared against.
    /// </summary>
    /// <remarks>
    /// Both text search configurations are built rather than one being chosen,
    /// because a query is too short to say what language it is in — "Pasta",
    /// "Curry" and "Butter" are both at once — while a document says so
    /// outright. The row picks which of the two to use.
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
    /// Whether the query matched inside one weight band of the document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A tsquery answers whether, never where, and the tier needs where — so
    /// the vector is cut down to the one band and asked again. This used to be
    /// a cover-density rank with every weight but one set to zero, which gives
    /// the same answer for every document the query matches and cost thirteen
    /// microseconds a row where this costs well under one; over the thousands
    /// of candidates a common word brings in, it was most of the query.
    /// </para>
    /// <para>
    /// Only a document the query matches can match in a band, which is the
    /// one place this means something different from the rank it replaces: a
    /// query with a minus in it. The rank never evaluated the minus, so a
    /// recipe containing the excluded word still earned credit for the rest;
    /// here it does not, which is what "-reis" was asking for. A query that is
    /// nothing but exclusions has no band to have matched in, and says so
    /// through <c>querytree</c>, whose answer for such a query is <c>T</c>.
    /// </para>
    /// </remarks>
    private static string BandHit(char weight) =>
        $"(d.document @@ lang.tsq and querytree(lang.tsq) <> 'T' "
        + $"and ts_filter(d.document, '{{{weight}}}') @@ lang.tsq)";

    /// <summary>
    /// The cover-density rank of this row, bounded into [0, 1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Normalisation 32 is <c>rank / (rank + 1)</c>, which is what makes the
    /// value comparable with the other terms of the score. Length
    /// normalisation is deliberately not asked for on top of it: how much of
    /// the title the query accounts for is already a term of its own, and
    /// dividing by the document length here would count that twice.
    /// </para>
    /// <para>
    /// Zero without asking when the document does not match, which is most
    /// candidates — the substring lane brings in every compound, and a
    /// compound is exactly what the stemmer cannot see. <c>ts_rank_cd</c> on a
    /// document it cannot match still searches the whole of it for a cover,
    /// and that search was the most expensive thing a broad query did. It
    /// changes nothing but a query with a minus in it, for the reason given
    /// under <see cref="BandHit"/>.
    /// </para>
    /// </remarks>
    private const string Rank = """
        case when d.document @@ lang.tsq
             then ts_rank_cd('{0.1, 0.3, 0.6, 1.0}'::float4[], d.document, lang.tsq, 32)
             else 0 end
        """;

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
                   or strict_word_similarity(term, d.title_ae) >= @fuzzyThreshold)
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
    /// Which recipes are candidates at all: one index scan per lane, and the
    /// ids they found.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The recipes a query matches are the union of what each lane matches, and
    /// it is asked as a union because that is the only form PostgreSQL can
    /// answer from the indexes. The same lanes written as one <c>or</c> — which
    /// this was — are one condition no index serves, so every text search read
    /// every document in the household: 470 ms for "Tomaten Reis" over ten
    /// thousand recipes. Each branch below is one lane over one index.
    /// </para>
    /// <para>
    /// The query is spelt out in every branch rather than read from the
    /// <c>q</c> row, and that is what makes the indexes usable at all.
    /// Npgsql sends <c>@query</c> as a parameter of an unnamed statement, which
    /// PostgreSQL plans knowing its value, and the fold functions are immutable,
    /// so <c>culina_fold_ae(@query)</c> is worked out while planning and the
    /// title's prefix scan becomes a range of the btree. A column of a CTE is
    /// only known once the query runs, too late for a range.
    /// </para>
    /// <para>
    /// Every lane is here, including the ones the ranking treats as weak.
    /// Narrowing the candidate set is the one thing that cannot be undone
    /// later: a recipe this leaves out is one no amount of ranking can bring
    /// back. Two lanes of the old <c>or</c> are not listed because they are
    /// already inside the substring lane — a title is the first thing in
    /// <c>fuzzy_text</c>, in both folds, so a title holding a term is a
    /// document holding it. That stops being true only for a query with no
    /// term in it at all ("Ei", "und"), and those still read the titles.
    /// </para>
    /// <para>
    /// A recipe with no document row is excluded from a text search rather than
    /// from the collection, because every lane reads the documents: it still
    /// lists, still filters and still pages, and only stops being findable by
    /// words until the next write rebuilds it. A missing document should be
    /// impossible — the migration backfills every row and the writer runs in
    /// the recipe's own transaction — but "impossible" is a poor reason for a
    /// recipe to vanish from a library.
    /// </para>
    /// </remarks>
    internal const string Hits = $"""
        -- Lexical, by the document's own language: what the stemmer can see.
        select d.recipe_id from recipe_search_documents d
        where d.household_id = @householdId and d.language = 'de'
          and d.document @@ websearch_to_tsquery('culina_de', @query::text)
        union
        select d.recipe_id from recipe_search_documents d
        where d.household_id = @householdId and d.language <> 'de'
          and d.document @@ websearch_to_tsquery('culina_en', @query::text)
        union
        -- A term inside any word of the recipe: compounds, everywhere.
        select d.recipe_id
        from unnest(culina_search_terms(@query::text)) as term
        join recipe_search_documents d on d.fuzzy_text like '%' || term || '%'
        where d.household_id = @householdId
        union
        -- A term near enough to a word of the title: typos. The operator is
        -- the index's way in and the comparison after it is the rule — see
        -- migration 0018 for why the two are separate.
        select d.recipe_id
        from unnest(culina_search_terms(@query::text)) as term
        join recipe_search_documents d on term <<% d.title_ae
        where d.household_id = @householdId
          and strict_word_similarity(term, d.title_ae) >= @fuzzyThreshold
        union
        -- What the recipe is rather than what it says: every concept the
        -- query names, among the ones its title, tags and ingredients do.
        -- Waffeln for "Nachtisch", Hähnchen for "chicken". Every, not any:
        -- "Hähnchen Reis" is a chicken dish with rice, not every dish with
        -- either. An empty array is contained in everything, so a query the
        -- lexicon cannot read is kept out by name rather than by accident.
        select d.recipe_id from recipe_search_documents d
        where d.household_id = @householdId
          and cardinality(@concepts::text[]) > 0
          and d.concepts @> @concepts::text[]
        union
        -- The title, for a query too short to have a term of its own.
        select d.recipe_id from q
        cross join recipe_search_documents d
        where cardinality(culina_search_terms(@query::text)) = 0
          and d.household_id = @householdId
          and ({TitleWord})
        """;

    /// <summary>
    /// Why each candidate is a candidate, recorded per row for the tier.
    /// </summary>
    internal static string Evidence { get; } = $"""
        coalesce({ExactTitle}, false)                    as exact_title,
        coalesce({TitleWord}, false)                     as title_word,
        coalesce({BandHit('a')}, false)                  as title_hit,
        coalesce({BandHit('b')}, false)                  as tag_hit,
        coalesce(d.document @@ lang.tsq, false)          as lexical_hit,
        coalesce({FuzzyTitle}, false)                    as fuzzy_title,
        coalesce({FuzzyBody}, false)                     as fuzzy_body,
        coalesce(cardinality(@concepts::text[]) > 0 and d.concepts @> @concepts::text[], false) as concept_hit,
        coalesce(cardinality(@diets::text[]) > 0 and d.concepts @> @diets::text[], false) as diet_asserted,
        {TitleSimilarity}                                as title_similarity,
        coalesce({Rank}, 0)::float8                      as lexical_rank,
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
    /// <para>
    /// A match through the lexicon alone is last (tier 5), below a word merely
    /// found somewhere in the recipe. The lexicon is a guess about what words
    /// mean and a substring is a fact about what the recipe says, so however
    /// right the guess is, it never outranks the fact — and when it is wrong,
    /// the wrong recipe is at the bottom of the list rather than at the top.
    /// </para>
    /// </remarks>
    internal const string Tier = """
        case
            when not has_text                  then 0
            when exact_title                   then 0
            when title_word or title_hit       then 1
            when fuzzy_title or tag_hit        then 2
            when lexical_hit                   then 3
            when fuzzy_body                    then 4
            else                                    5
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
    /// Three nudges sit outside the four weights, each zero unless its
    /// question was asked: "schnell" (<see cref="QuickFit"/>), the closer
    /// spelling within the typo tier (<see cref="TitleSimilarity"/>), and a
    /// diet somebody asserted — a title or a tag that says vegetarisch — ahead
    /// of one that is only presumed because nothing in the recipe refutes it.
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
        + 0.10 * quick_fit
        + 0.10 * title_similarity
        + 0.10 * case when diet_asserted then 1.0 else 0.0 end
        """;

    /// <summary>
    /// How nearly a term of the query is a whole word of the title.
    /// </summary>
    /// <remarks>
    /// The typo lane puts every title a misspelling resembles into one tier,
    /// and "Kartoffelgratn" resembles Kartoffelsalat as well as Kartoffelgratin
    /// — so within the tier, the closer spelling comes first. Measured against
    /// the title only, like the lane itself, which keeps it a few microseconds
    /// a row. Outside the four weights, and one for an exact title, so it only
    /// ever breaks ties the tier and the coverage left.
    /// </remarks>
    private const string TitleSimilarity = """
        coalesce((select max(strict_word_similarity(term, d.title_ae)) from unnest(q.terms) as term), 0)::float8
        """;

    /// <summary>
    /// How well a recipe answers "schnell", when that was asked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A preference and so a nudge, never a filter. "unter 30 Minuten" states a
    /// number and means it; "schnell" states an intent, and turning it into
    /// thirty minutes would silently drop the twenty-five-minute recipe
    /// nobody wrote a time on. So a recipe known to be quick is lifted, one
    /// with no stated time is lifted half as far, and nothing is removed.
    /// </para>
    /// <para>
    /// Outside the four weights above, which sum to one: zero whenever
    /// nobody asked, so it changes no order but the one it was asked for.
    /// </para>
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
