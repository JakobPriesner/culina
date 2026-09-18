using Application.Abstractions;
using Domain.Suggestions;

namespace Infrastructure.Persistence.Suggestions;

/// <summary>One scored candidate as PostgreSQL returns it.</summary>
/// <remarks>
/// Every term comes back separately rather than pre-summed, which is the whole
/// of how an explanation stays a fact: the reason is whichever term dominated,
/// not a sentence chosen to suit a recipe that was picked for other reasons.
/// </remarks>
internal sealed record SuggestionRowData
{
    public Guid RecipeId { get; init; }

    public string Title { get; init; } = string.Empty;

    public Guid? ImageId { get; init; }

    public int? TotalMinutes { get; init; }

    public decimal YieldAmount { get; init; }

    public string YieldKind { get; init; } = "servings";

    public string? YieldLabel { get; init; }

    public string[] Tags { get; init; } = [];

    public int CookCount { get; init; }

    public DateTimeOffset? LastCookedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public int MatchedIngredients { get; init; }

    public int IngredientCount { get; init; }

    public string[] Features { get; init; } = [];

    public decimal Score { get; init; }

    public decimal Affinity { get; init; }

    public decimal Content { get; init; }

    public decimal Repetition { get; init; }

    public decimal Rediscovery { get; init; }

    public decimal Slot { get; init; }

    public decimal Effort { get; init; }

    public decimal Household { get; init; }

    public decimal Novelty { get; init; }

    public decimal Freshness { get; init; }

    public decimal Season { get; init; }

    public decimal Similarity { get; init; }

    public decimal Exploration { get; init; }

    public string? TagSubject { get; init; }

    public string? IngredientSubject { get; init; }

    public string? HouseholdSubject { get; init; }
}

/// <summary>
/// Runs the scoring query and hands back candidates, best first.
/// </summary>
/// <remarks>
/// <para>
/// Split from <see cref="SuggestionRanker"/> so that "what the database is
/// asked" and "how the answer is chosen from" are two things you can read
/// separately — the same reason the recipe search keeps its ordering in its own
/// file.
/// </para>
/// <para>
/// <b>What filters here and what only ranks.</b> A filter is something the
/// caller asked for: a time ceiling, tags, ingredients to use up, recipes they
/// already have on screen. A dismissal is the same thing said earlier. Nothing
/// the system decides by itself is ever a filter — being cooked yesterday is a
/// heavy penalty and never an exclusion — because a system-imposed filter is
/// how a ranker ends up returning nothing and being unable to say why.
/// </para>
/// </remarks>
/// <param name="executor">Runs the SQL inside the request's transaction.</param>
/// <param name="weights">What each term is worth.</param>
internal sealed class SuggestionReader(DbExecutor executor, RankingWeights weights)
{
    internal async Task<IReadOnlyList<ScoredRecipe>> ScoreAsync(
        SuggestionContext context,
        CancellationToken cancellationToken)
    {
        var sql = $$"""
            with {{SuggestionScoringSql.Ctes}}
            select
                r.id as recipe_id,
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
                coalesce(mine.cook_count, 0) as cook_count,
                mine.last_cooked_at,
                (select count(*) from recipe_ingredients ri
                 join ingredient_groups g on g.id = ri.group_id
                 where g.recipe_id = r.id) as ingredient_count,
                (select count(*) from unnest(@ingredients::text[]) as wanted
                 where exists (
                     select 1 from recipe_ingredients ri
                     join ingredient_groups g on g.id = ri.group_id
                     where g.recipe_id = r.id and ri.name ilike '%' || wanted || '%'))
                    as matched_ingredients,
                -- What the diversity pass compares. Bounded, because a recipe
                -- with sixty ingredients should not cost sixty string
                -- comparisons per candidate pair for a number that would not
                -- move.
                coalesce(
                    array(
                        select f.kind || ':' || f.feature
                        from sc_features f
                        where f.recipe_id = r.id
                        order by f.kind, f.feature
                        limit 40),
                    '{}') as features,
                s.score, s.affinity, s.content, s.repetition, s.rediscovery, s.slot, s.effort,
                s.household, s.novelty, s.freshness, s.season, s.similarity, s.exploration,
                s.tag_subject, s.ingredient_subject, s.household_subject
            from recipes r
            join suggestion_scores s on s.recipe_id = r.id
            -- One scan for both facts, rather than two subqueries over the same
            -- index. cook_log_recipe_user_idx is (recipe_id, user_id, made_at desc).
            left join lateral (
                select count(*) as cook_count, max(c.made_at) as last_cooked_at
                from cook_log_entries c
                where c.recipe_id = r.id and c.user_id = @userId::uuid
            ) mine on true
            where r.household_id = @householdId::uuid
              and not s.dismissed
              and r.id <> all (@excluded::uuid[])
              and (@likeRecipeId::uuid is null or r.id <> @likeRecipeId::uuid)
              -- The recipe search's own clause, including its refusal to treat
              -- an unknown time as zero: "I have 25 minutes" asks for recipes
              -- known to fit, and an unknown time is not an answer.
              and (@maxMinutes::int is null
                   or ((r.prep_minutes is not null or r.cook_minutes is not null)
                       and coalesce(r.prep_minutes, 0) + coalesce(r.cook_minutes, 0) <= @maxMinutes::int))
              and (@tagCount::int = 0 or (
                    select count(distinct t.slug) from recipe_tags rt
                    join tags t on t.id = rt.tag_id
                    where rt.recipe_id = r.id and t.slug = any(@tags::text[])) = @tagCount::int)
              -- Naming ingredients in a suggestion means "about these", so at
              -- least one has to be in it. The recipe search ranks by them
              -- instead, because there the list is the whole library and a
              -- recipe without chicken is a worse match rather than not an
              -- answer.
              and (@ingredientCount::int = 0 or exists (
                    select 1 from unnest(@ingredients::text[]) as wanted
                    join ingredient_groups g on g.recipe_id = r.id
                    join recipe_ingredients ri on ri.group_id = g.id
                    where ri.name ilike '%' || wanted || '%'))
            order by s.score desc, r.id desc
            limit @pool::int;
            """;

        var parameters = SuggestionScoringSql.Parameters(context, weights);

        parameters.Add("householdId", context.HouseholdId);
        parameters.Add("userId", context.UserId);
        parameters.Add("likeRecipeId", context.LikeRecipeId);
        parameters.Add("excluded", context.Exclude.ToArray());
        parameters.Add("maxMinutes", context.MaxMinutes);
        parameters.Add("tags", context.Tags.Distinct(StringComparer.Ordinal).ToArray());
        parameters.Add("tagCount", context.Tags.Distinct(StringComparer.Ordinal).Count());
        parameters.Add("ingredients", context.Ingredients.ToArray());
        parameters.Add("ingredientCount", context.Ingredients.Count);
        parameters.Add("pool", context.PoolSize);

        var rows = await executor
            .QueryAsync<SuggestionRowData>(sql, parameters, cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(ToScored)];
    }

    private static ScoredRecipe ToScored(SuggestionRowData row) => new(
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
            row.MatchedIngredients,
            row.IngredientCount,
            AddedToCookbookAt: null),
        row.Score,
        Terms(row),
        row.Features);

    /// <summary>
    /// The terms that contributed, largest first.
    /// </summary>
    /// <remarks>
    /// Zero-valued terms are dropped: a term that did nothing is not a reason
    /// it was almost shown for, and keeping them would make the list of "what
    /// decided this" mostly noise.
    /// </remarks>
    private static List<ScoreTerm> Terms(SuggestionRowData row)
    {
        List<ScoreTerm> terms =
        [
            new(SuggestionReason.Affinity, row.Affinity, null),
            new(SuggestionReason.Tag, row.Content, row.TagSubject),
            new(SuggestionReason.Rediscovery, row.Rediscovery, null),
            new(SuggestionReason.Season, row.Season, null),
            new(SuggestionReason.Slot, row.Slot, null),
            new(SuggestionReason.Household, row.Household, row.HouseholdSubject),
            new(SuggestionReason.Fresh, row.Novelty + row.Freshness, null),
            new(SuggestionReason.Similar, row.Similarity, null)
        ];

        // Content is one number over two kinds of feature, so the reason picks
        // whichever kind actually named something. An ingredient is the more
        // concrete of the two — "you cook a lot with aubergine" says more than
        // a tag somebody typed once — so it wins when both are available.
        if (row.IngredientSubject is not null && row.Content > 0)
        {
            terms[1] = new ScoreTerm(SuggestionReason.Ingredient, row.Content, row.IngredientSubject);
        }

        // The repetition penalty ranks but never explains: "you had this on
        // Tuesday" is a reason something is NOT being suggested, and printing
        // it on a card that is being suggested would be nonsense.
        return [.. terms.Where(term => term.Contribution > 0).OrderByDescending(term => term.Contribution)];
    }
}
