using Application.Abstractions;
using Infrastructure.Persistence.Cookbooks;

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

    public string[] Tags { get; init; } = [];

    public int CookCount { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public int MatchedIngredients { get; init; }

    public int IngredientCount { get; init; }

    public int TotalCount { get; init; }

    public DateTimeOffset? AddedToCookbookAt { get; init; }
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
/// </remarks>
/// <param name="executor">Runs the SQL.</param>
internal sealed class RecipeSearcher(DbExecutor executor)
{
    /// <summary>A hard ceiling, enforced here and not only in the endpoint.</summary>
    internal const int MaxLimit = 100;

    private static readonly string Projection = $$"""
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
            r.updated_at,
            coalesce(
                array(
                    select t.slug from recipe_tags rt
                    join tags t on t.id = rt.tag_id
                    where rt.recipe_id = r.id
                    order by t.slug),
                '{}') as tags,
            (select count(*) from cook_log_entries c
             where c.recipe_id = r.id and c.user_id = @userId) as cook_count,
            (select count(*) from recipe_ingredients ri
             join ingredient_groups g on g.id = ri.group_id
             where g.recipe_id = r.id) as ingredient_count,
            (select count(*) from unnest(@ingredients::text[]) as wanted
             where exists (
                 select 1 from recipe_ingredients ri
                 join ingredient_groups g on g.id = ri.group_id
                 where g.recipe_id = r.id and ri.name ilike '%' || wanted || '%'))
                as matched_ingredients,
            (select cr.added_at from cookbook_recipes cr
             where cr.cookbook_id = @cookbookId and cr.recipe_id = r.id) as added_to_cookbook_at
        from recipes r
        where r.household_id = @householdId
          and (@query is null or (
                r.title ilike @queryLike
                or r.description ilike @queryLike
                or exists (
                    select 1 from recipe_ingredients ri
                    join ingredient_groups g on g.id = ri.group_id
                    where g.recipe_id = r.id and ri.name ilike @queryLike)))
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

        // One extra row tells us whether there is a next page without a second
        // count query.
        var sql = $"""
            with matching as ({Projection}),
            ranked as (
                select *, ingredient_count - matched_ingredients as extra_ingredients
                from matching),
            counted as (select *, count(*) over () as total_count from ranked)
            select * from counted
            {(resume is null ? string.Empty : $"where {resume}")}
            order by {RecipeSearchSql.OrderBy(search.Sort)}
            limit {limit + 1};
            """;

        var rows = await executor.QueryAsync<RecipeSearchRowData>(
            sql,
            Parameters(search, cursor),
            cancellationToken).ConfigureAwait(false);

        var page = rows.Take(limit).ToList();

        return new RecipePage(
            [.. page.Select(ToRow)],
            NextCursorFor(search.Sort, rows.Count > limit, page),
            rows.Count == 0 ? 0 : rows[0].TotalCount);
    }

    private static object Parameters(RecipeSearch search, RecipeCursor? cursor)
    {
        // Counted after de-duplication, because the clause compares this to a
        // count of distinct slugs: ?tag=quick&tag=quick would otherwise ask for
        // two of a tag a recipe can only carry once, and match nothing.
        var tags = search.Tags.Distinct(StringComparer.Ordinal).ToArray();

        return new
        {
            householdId = search.HouseholdId,
            userId = search.UserId,
            query = string.IsNullOrWhiteSpace(search.Query) ? null : search.Query.Trim(),
            queryLike = $"%{search.Query?.Trim()}%",
            tags,
            tagCount = tags.Length,
            ingredients = search.Ingredients.ToArray(),
            maxMinutes = search.MaxMinutes,
            cookbookId = search.CookbookId,
            ruleTags = search.Rules?.Tags.Distinct(StringComparer.Ordinal).ToArray() ?? [],
            ruleIngredients = search.Rules?.Ingredients.ToArray() ?? [],
            ruleMaxMinutes = search.Rules?.MaxMinutes,
            cursorId = cursor?.Id ?? Guid.Empty,
            k0 = KeyAt(cursor, 0),
            k1 = KeyAt(cursor, 1),
            k2 = KeyAt(cursor, 2)
        };
    }

    private static string KeyAt(RecipeCursor? cursor, int index) =>
        cursor is not null && index < cursor.Keys.Count ? cursor.Keys[index] : string.Empty;

    private static string? NextCursorFor(
        RecipeSort sort,
        bool hasMore,
        List<RecipeSearchRowData> page) =>
        hasMore && page.Count > 0
            ? new RecipeCursor(sort, RecipeSearchSql.KeysOf(sort, ToRow(page[^1])), page[^1].Id).Encode()
            : null;

    private static RecipeSearchRow ToRow(RecipeSearchRowData data) => new(
        data.Id,
        data.Title,
        data.ImageId,
        data.TotalMinutes,
        data.YieldAmount,
        data.YieldKind,
        data.Tags,
        data.CookCount,
        data.UpdatedAt,
        data.MatchedIngredients,
        data.IngredientCount,
        data.AddedToCookbookAt);
}
