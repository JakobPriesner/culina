using Application.Abstractions;
using Dapper;
using Domain.Suggestions;
using Infrastructure.Persistence.Cookbooks;
using Infrastructure.Persistence.Suggestions;

namespace Infrastructure.Persistence.Recipes;

/// <summary>One row of the search projection as PostgreSQL returns it.</summary>
internal sealed record RecipeSearchRowData
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public Guid? ImageId { get; init; }

    public int? TotalMinutes { get; init; }

    public decimal YieldAmount { get; init; }

    public string YieldKind { get; init; } = "servings";

    public string? YieldLabel { get; init; }

    public string[] Tags { get; init; } = [];

    public int CookCount { get; init; }

    public DateTimeOffset? LastCookedAt { get; init; }

    /// <summary>Zero for every sort but <see cref="RecipeSort.Suggested"/>.</summary>
    public decimal SuggestionScore { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public int MatchedIngredients { get; init; }

    public int IngredientCount { get; init; }

    public int TotalCount { get; init; }

    public DateTimeOffset? AddedToCookbookAt { get; init; }

    /// <summary>Which kind of evidence put this row here. Lower is stronger.</summary>
    public int Tier { get; init; }

    /// <summary>How well it fits, within its tier.</summary>
    public double Score { get; init; }
}

/// <summary>
/// Finds recipes.
/// </summary>
/// <remarks>
/// <para>
/// One query does the filtering, the ranking, the count and the page. Splitting
/// it would mean the count and the page could disagree when something changes
/// between them, and a "showing 20 of 19" is the kind of small wrongness people
/// notice.
/// </para>
/// <para>
/// Ingredient matching is the whole of Culina's "what can I cook?" feature.
/// There is no pantry to maintain — the caller names two or three things they
/// want to use up, and the ranking does the rest — which is precisely why it
/// cannot go stale.
/// </para>
/// <para>
/// Free text is answered from <c>recipe_search_documents</c> rather than by
/// scanning the recipe tables, through the lanes in
/// <see cref="RecipeSearchLanes"/>. The join is a left join on purpose: a
/// recipe whose document is somehow missing still lists, still filters and
/// still pages, and is only unfindable by words until the next write rebuilds
/// it.
/// </para>
/// </remarks>
/// <param name="executor">Runs the SQL.</param>
/// <param name="time">The clock the suggested order is ranked against.</param>
/// <param name="weights">What each term of the suggested order is worth.</param>
internal sealed class RecipeSearcher(DbExecutor executor, TimeProvider time, RankingWeights weights)
{
    /// <summary>A hard ceiling, enforced here and not only in the endpoint.</summary>
    internal const int MaxLimit = 100;

    /// <summary>
    /// How alike two words have to be before one counts as the other misspelt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A constant rather than a setting. It is a relevance parameter, not an
    /// operational one: changing it changes which results people see, so it
    /// belongs in a commit next to the test case that moved it rather than in
    /// an environment variable nobody reviews.
    /// </para>
    /// <para>
    /// Half, and the number is not a guess. Trigram similarity counts shared
    /// three-letter windows, and a single transposed or missing letter in the
    /// middle of a word destroys three of them at once: "Bolgnese" scores
    /// 0.58 against "Bolognese" and "Bolognäse" scores 0.54, so anything
    /// stricter refuses both of the misspellings this was built to survive.
    /// The cost of being generous is contained by where it lands — a match
    /// found only this way is two tiers down, below everything the query
    /// actually names.
    /// </para>
    /// </remarks>
    private const double FuzzyThreshold = 0.5d;

    /// <summary>
    /// The projection, built once per shape rather than per request.
    /// </summary>
    /// <remarks>
    /// Two constant strings, because only the suggested order pays for the
    /// scoring join and only a plain browse should pay for neither.
    /// </remarks>
    private static readonly string ScoredProjection = Projection(scored: true);

    private static readonly string PlainProjection = Projection(scored: false);

    private static string Projection(bool scored) => $$"""
        select
            r.id,
            r.title,
            r.image_id,
            case
                when r.prep_minutes is null and r.cook_minutes is null then null
                else coalesce(r.prep_minutes, 0) + coalesce(r.cook_minutes, 0)
            end as total_minutes,
            r.yield_amount,
            r.yield_kind,
            r.yield_label,
            r.updated_at,
            coalesce(
                array(
                    select t.slug from recipe_tags rt
                    join tags t on t.id = rt.tag_id
                    where rt.recipe_id = r.id
                    order by t.slug),
                '{}') as tags,
            -- One scan for both facts rather than two over the same index:
            -- cook_log_recipe_user_idx is (recipe_id, user_id, made_at desc),
            -- so the count and the latest entry come out of one lookup.
            coalesce(mine.cook_count, 0) as cook_count,
            mine.last_cooked_at,
            {{(scored ? "coalesce(s.score, 0)" : "0::numeric")}} as suggestion_score,
            -- From the document, which counted them when the recipe was
            -- written. The subquery is the fallback for a document that has
            -- gone missing, and coalesce only reaches it when one has: counting
            -- ingredients per row was 620 ms over two thousand recipes, and it
            -- was the most expensive thing in this query long before search
            -- was rewritten.
            coalesce(d.ingredient_count, (
                select count(*) from recipe_ingredients ri
                join ingredient_groups g on g.id = ri.group_id
                where g.recipe_id = r.id)) as ingredient_count,
            (select count(*) from unnest(@ingredients::text[]) as wanted
             where exists (
                 select 1 from recipe_ingredients ri
                 join ingredient_groups g on g.id = ri.group_id
                 where g.recipe_id = r.id and ri.name ilike '%' || wanted || '%'))
                as matched_ingredients,
            (select cr.added_at from cookbook_recipes cr
             where cr.cookbook_id = @cookbookId and cr.recipe_id = r.id) as added_to_cookbook_at,
            q.has_text,
            {{RecipeSearchLanes.Evidence}}
        from q
        cross join recipes r
        left join recipe_search_documents d on d.recipe_id = r.id
        left join lateral (
            select count(*) as cook_count, max(c.made_at) as last_cooked_at
            from cook_log_entries c
            where c.recipe_id = r.id and c.user_id = @userId
        ) mine on true
        {{(scored ? "left join suggestion_scores s on s.recipe_id = r.id" : string.Empty)}}
        {{RecipeSearchLanes.LanguageJoin}}
        where r.household_id = @householdId
          -- Hidden from the suggested order and from nowhere else: the recipe
          -- is still the household's, still searchable and still on its
          -- shelves. "Stop suggesting this" is not "delete this".
          {{(scored ? "and not coalesce(s.dismissed, false)" : string.Empty)}}
          -- Words are answered by the search document, in four lanes, rather
          -- than by three LIKE scans over the recipe tables. The lanes are
          -- named in RecipeSearchLanes because the tier below has to know
          -- which of them fired.
          and (@query::text is null or not q.has_text or ({{RecipeSearchLanes.Predicate}}))
          and (@tagCount = 0 or (
                select count(distinct t.slug) from recipe_tags rt
                join tags t on t.id = rt.tag_id
                where rt.recipe_id = r.id and t.slug = any(@tags::text[])) = @tagCount)
          -- A shelf is a filter over the collection, not a second collection.
          -- Everything else here — the search, the tags, the time ceiling, the
          -- ingredient ranking — therefore works inside a cookbook for free,
          -- and a cookbook belonging to another household matches nothing
          -- because the household predicate above has already applied.
          and (@cookbookId is null or exists (
                select 1 from cookbook_recipes cr
                where cr.cookbook_id = @cookbookId and cr.recipe_id = r.id))
          -- A shelf that fills itself, asked here rather than remembered
          -- anywhere: this is the whole of "a new recipe appears on it by
          -- itself". The rules live in SmartShelfSql because the cookbook card
          -- counts the same recipes this lists, and the two must not drift.
          and {{SmartShelfSql.Matches("@ruleTags::text[]", "@ruleIngredients::text[]", "@ruleMaxMinutes")}}
          -- A recipe with no stated time is excluded by a time filter rather
          -- than treated as taking zero minutes. "I have 25 minutes" asks for
          -- recipes known to fit, and an unknown time is not an answer.
          and (@maxMinutes is null
               or ((r.prep_minutes is not null or r.cook_minutes is not null)
                   and coalesce(r.prep_minutes, 0) + coalesce(r.cook_minutes, 0) <= @maxMinutes))
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

        // Joined in only when it is what the order asks for. Ninety lines of
        // common table expressions on every plain browse would be work done to
        // multiply by zero.
        var scoring = scored ? $"{SuggestionScoringSql.Ctes},\n            " : string.Empty;

        // One extra row tells us whether there is a next page without a second
        // count query.
        var sql = $"""
            with {scoring}q as ({RecipeSearchLanes.QueryCte}),
            matching as ({(scored ? ScoredProjection : PlainProjection)}),
            ranked as (
                select *,
                    ingredient_count - matched_ingredients as extra_ingredients,
                    {RecipeSearchLanes.Tier} as tier,
                    {RecipeSearchLanes.StructuralFit} as structural_fit
                from matching),
            scored as (select *, {RecipeSearchLanes.Score} as score from ranked),
            counted as (select *, count(*) over () as total_count from scored)
            select * from counted
            {(resume is null ? string.Empty : $"where {resume}")}
            order by {RecipeSearchSql.OrderBy(search.Sort)}
            limit {limit + 1};
            """;

        var rows = await executor.QueryAsync<RecipeSearchRowData>(
            sql,
            Parameters(search, cursor, scored),
            cancellationToken).ConfigureAwait(false);

        var page = rows.Take(limit).ToList();

        return new RecipePage(
            [.. page.Select(ToRow)],
            NextCursorFor(search.Sort, rows.Count > limit, page),
            rows.Count == 0 ? 0 : rows[0].TotalCount);
    }

    private DynamicParameters Parameters(RecipeSearch search, RecipeCursor? cursor, bool scored)
    {
        // Counted after de-duplication, because the clause compares this to a
        // count of distinct slugs: ?tag=quick&tag=quick would otherwise ask for
        // two of a tag a recipe can only carry once, and match nothing.
        var tags = search.Tags.Distinct(StringComparer.Ordinal).ToArray();
        var ingredients = search.Ingredients.ToArray();

        var parameters = new DynamicParameters(new
        {
            householdId = search.HouseholdId,
            userId = search.UserId,
            query = string.IsNullOrWhiteSpace(search.Query) ? null : search.Query.Trim(),
            fuzzyThreshold = FuzzyThreshold,
            tags,
            tagCount = tags.Length,
            ingredients,
            ingredientCount = ingredients.Length,
            maxMinutes = search.MaxMinutes,
            cookbookId = search.CookbookId,
            ruleTags = search.Rules?.Tags.Distinct(StringComparer.Ordinal).ToArray() ?? [],
            ruleIngredients = search.Rules?.Ingredients.ToArray() ?? [],
            ruleMaxMinutes = search.Rules?.MaxMinutes,
            cursorId = cursor?.Id ?? Guid.Empty,
            k0 = KeyAt(cursor, 0),
            k1 = KeyAt(cursor, 1),
            k2 = KeyAt(cursor, 2)
        });

        if (scored)
        {
            // The same occasion the suggestion endpoint builds, minus the parts
            // a library listing cannot know: no slot, nothing to resemble,
            // nothing on screen to avoid. Browse also turns the exploration
            // jitter off, because a jittered order is not one a cursor can
            // resume.
            parameters.AddDynamicParams(
                SuggestionScoringSql.Parameters(
                    new SuggestionContext(
                        search.HouseholdId,
                        search.UserId,
                        SuggestionPurpose.Browse,
                        Today(),
                        Slot: null,
                        search.MaxMinutes,
                        search.Tags,
                        search.Ingredients,
                        LikeRecipeId: null,
                        Exclude: [],
                        search.Limit),
                    weights));
        }

        return parameters;
    }

    /// <summary>
    /// The day being ranked for, not the instant.
    /// </summary>
    /// <remarks>
    /// Every decayed term is a function of this, so two requests on the same day
    /// score identically — which is what lets a cursor resume the order it was
    /// cut from, and what stops the list reordering under somebody who is still
    /// reading it.
    /// </remarks>
    private DateTimeOffset Today()
    {
        var now = time.GetUtcNow();

        return new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
    }

    private static string KeyAt(RecipeCursor? cursor, int index) =>
        cursor is not null && index < cursor.Keys.Count ? cursor.Keys[index] : string.Empty;

    private static string? NextCursorFor(
        RecipeSort sort,
        bool hasMore,
        List<RecipeSearchRowData> page) =>
        hasMore && page.Count > 0
            ? new RecipeCursor(sort, RecipeSearchSql.KeysOf(sort, page[^1]), page[^1].Id).Encode()
            : null;

    private static RecipeSearchRow ToRow(RecipeSearchRowData data) => new(
        data.Id,
        data.Title,
        data.ImageId,
        data.TotalMinutes,
        data.YieldAmount,
        data.YieldKind,
        data.YieldLabel,
        data.Tags,
        data.CookCount,
        data.LastCookedAt,
        data.UpdatedAt,
        data.MatchedIngredients,
        data.IngredientCount,
        data.AddedToCookbookAt);
}
