using Application.Abstractions;
using Domain.Suggestions;
using Infrastructure.Persistence.Recipes;

namespace Infrastructure.Persistence.Suggestions;

/// <summary>
/// One scored candidate as PostgreSQL returns it; every term comes back separate, so the reason is
/// whichever term dominated, not a sentence chosen to suit the pick.
/// </summary>
internal sealed record SuggestionRowData : RecipeCardRow
{
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

/// <summary>Runs the scoring query and hands back candidates, best first.</summary>
/// <remarks>
/// Filters are only what the caller asked for (time ceiling, tags, ingredients, recipes on screen,
/// dismissals); nothing the system decides is a filter, since that is how a ranker returns nothing
/// and cannot say why.
/// </remarks>
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
                r.household_id,
                r.title,
                r.image_id,
                {{RecipeSql.TotalMinutes()}} as total_minutes,
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
                -- Bounded: sixty ingredients should not cost sixty string comparisons per candidate
                -- pair.
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
            -- One scan for both facts; cook_log_recipe_user_idx is (recipe_id, user_id, made_at
            -- desc).
            left join lateral (
                select count(*) as cook_count, max(c.made_at) as last_cooked_at
                from cook_log_entries c
                where c.recipe_id = r.id and c.user_id = @userId::uuid
            ) mine on true
            where r.household_id = any(@library::uuid[])
              and not s.dismissed
              and r.id <> all (@excluded::uuid[])
              and (@likeRecipeId::uuid is null or r.id <> @likeRecipeId::uuid)
              -- The recipe search's clause: an unknown time is not an answer to "I have 25
              -- minutes".
              and (@maxMinutes::int is null
                   or {{RecipeSql.FitsWithin("@maxMinutes::int")}})
              and (@tagCount::int = 0 or (
                    select count(distinct t.slug) from recipe_tags rt
                    join tags t on t.id = rt.tag_id
                    where rt.recipe_id = r.id and t.slug = any(@tags::text[])) = @tagCount::int)
              -- Naming ingredients means "about these", so at least one must be in it (the search
              -- only ranks by them: there it is the whole library).
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
        parameters.Add("library", new[] { context.HouseholdId }.Concat(context.InheritedFrom).ToArray());
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
        row.ToSearchRow(),
        row.Score,
        Terms(row),
        row.Features);

    /// <summary>
    /// The terms that contributed, largest first; zero-valued terms are no reason and are dropped.
    /// </summary>
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

        // Content is one number over two kinds of feature; the reason picks whichever named
        // something, an ingredient first as the more concrete.
        if (row.IngredientSubject is not null && row.Content > 0)
        {
            terms[1] = new ScoreTerm(SuggestionReason.Ingredient, row.Content, row.IngredientSubject);
        }

        // The repetition penalty ranks but never explains: "you had this on Tuesday" is no reason
        // to suggest it.
        return [.. terms.Where(term => term.Contribution > 0).OrderByDescending(term => term.Contribution)];
    }
}
