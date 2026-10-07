using Application.Abstractions;
using Dapper;
using Domain.Suggestions;
using Infrastructure.Persistence.Planning;

namespace Infrastructure.Persistence.Suggestions;

/// <summary>The arithmetic behind "what should I cook?", as CTEs ending in <c>suggestion_scores</c>.</summary>
/// <remarks>
/// Kept apart so the search and the ranker share one ordering. Every term is a score and none a threshold:
/// a recipe eaten yesterday sinks, it is never removed, so the ranker returns everything eligible. Nothing
/// is materialised; scores are computed on read (a few ms per household), so there is no job or backfill.
/// </remarks>
internal static class SuggestionScoringSql
{
    /// <summary>The CTE block without the leading <c>with</c> and with a trailing comma.</summary>
    /// <remarks>Reads <c>@householdId</c>, <c>@library</c> and <c>@userId</c> plus the <c>sc*</c> and <c>w*</c> parameters from <see cref="Parameters"/>.</remarks>
    internal const string Ctes = """
        -- Every interaction that says something, in one shape, so weighting and decay are expressed once.
        sc_signals as (
            -- Cooking is the unit every other weight is measured against; photographing the result means more.
            select c.recipe_id,
                   c.user_id,
                   (case when c.image_hash is null then 1.0 else 1.3 end)::numeric as weight,
                   c.made_at as at,
                   true as decays
            from cook_log_entries c
            where c.household_id = @householdId::uuid

            union all

            -- Planning is intent, and belongs to the household; a null user feeds household popularity without
            -- becoming one person's taste.
            select m.recipe_id, null::uuid, 0.6, m.on_date::timestamptz, true
            from meal_plan_entries m
            where m.household_id = @householdId::uuid

            union all

            -- A shelf is a standing statement, not an event, so it does not decay.
            select cr.recipe_id, cr.added_by, 0.8, cr.added_at, false
            from cookbook_recipes cr
            join cookbooks cb on cb.id = cr.cookbook_id
            where cb.household_id = @householdId::uuid

            union all

            -- Nobody annotates a recipe they are indifferent to. On an inherited recipe only this household's notes count.
            select n.recipe_id, n.user_id, 0.4, n.updated_at, true
            from personal_notes n
            join recipes nr on nr.id = n.recipe_id
            where nr.household_id = @householdId::uuid
               or (nr.household_id = any(@library::uuid[])
                   and n.user_id in (
                       select hm.user_id from household_members hm
                       where hm.household_id = @householdId::uuid))

            union all

            -- Finishing what you started. Abandonment is deliberately absent: starting a session abandons the previous
            -- one, so it mostly reflects cooking something else and would punish the most-cooked recipes.
            select k.recipe_id, k.user_id, 0.3, k.completed_at, true
            from cook_sessions k
            where k.household_id = @householdId::uuid and k.completed_at is not null
        ),

        sc_weighted as (
            select s.recipe_id,
                   s.user_id,
                   s.weight * case
                       when s.decays then power(
                           0.5::numeric,
                           (greatest(extract(epoch from (@scAsOf::timestamptz - s.at)), 0) / 86400.0 / @scHalfLife::numeric)::numeric)
                       else 1
                   end as w
            from sc_signals s
            -- greatest(..., 0) rather than a filter: a planned meal is still intent, and a negative age would outrank cooking.
        ),

        -- What this person has done with each recipe. log1p: once vs twice is large, eleven vs twelve is nothing.
        sc_affinity as (
            select recipe_id, ln(1 + sum(w)) as value
            from sc_weighted
            where user_id = @userId::uuid
            group by recipe_id
        ),

        -- What everybody else has: just "your partner cooks this", not collaborative filtering.
        sc_household as (
            select recipe_id, ln(1 + sum(w)) as value
            from sc_weighted
            where user_id is distinct from @userId::uuid
            group by recipe_id
        ),

        -- Who, so the reason can name a person.
        sc_household_top as (
            select distinct on (c.recipe_id) c.recipe_id, u.display_name
            from cook_log_entries c
            join users u on u.id = c.user_id
            where c.household_id = @householdId::uuid and c.user_id <> @userId::uuid
            group by c.recipe_id, u.display_name
            order by c.recipe_id, count(*) desc, u.display_name
        ),

        -- When anyone in the household last made it. Household-wide on purpose: if your partner made it Tuesday,
        -- it should not be suggested to you Wednesday.
        sc_last_cooked as (
            select recipe_id, max(made_at) as at
            from cook_log_entries
            where household_id = @householdId::uuid and made_at <= @scAsOf::timestamptz
            group by recipe_id
        ),

        -- The only content features: household-authored tags and typed ingredient names.
        sc_features as (
            select r.id as recipe_id, 'tag' as kind, t.slug as feature
            from recipes r
            join recipe_tags rt on rt.recipe_id = r.id
            join tags t on t.id = rt.tag_id
            where r.household_id = any(@library::uuid[])

            union

            -- Folded as the shopping list folds a name and no further (stemming would be a third ingredient vocabulary).
            select r.id, 'ingredient', lower(unaccent(btrim(i.name)))
            from recipes r
            join ingredient_groups g on g.recipe_id = r.id
            join recipe_ingredients i on i.group_id = g.id
            where r.household_id = any(@library::uuid[]) and btrim(i.name) <> ''
        ),

        -- Inverse document frequency: a feature on every recipe (salt) scores exactly zero.
        sc_idf as (
            select f.kind,
                   f.feature,
                   ln((1 + (select count(*) from recipes where household_id = any(@library::uuid[]))::numeric)
                      / (1 + count(*))) as idf
            from sc_features f
            group by f.kind, f.feature
        ),

        -- This person's taste, in the same space the recipes live in.
        sc_taste as (
            select f.kind, f.feature, sum(a.value) * i.idf as weight
            from sc_affinity a
            join sc_features f on f.recipe_id = a.recipe_id
            join sc_idf i on i.kind = f.kind and i.feature = f.feature
            where a.value > 0
            group by f.kind, f.feature, i.idf
        ),

        sc_taste_norm as (select sqrt(coalesce(sum(weight * weight), 0)) as n from sc_taste),

        sc_recipe_norm as (
            select f.recipe_id, sqrt(sum(i.idf * i.idf)) as n
            from sc_features f
            join sc_idf i on i.kind = f.kind and i.feature = f.feature
            group by f.recipe_id
        ),

        -- Cosine between the two: the term that carries a recipe nobody has touched, so the app is useful in week one.
        sc_content as (
            select f.recipe_id, sum(t.weight * i.idf) / nullif(tn.n * rn.n, 0) as value
            from sc_features f
            join sc_idf i on i.kind = f.kind and i.feature = f.feature
            join sc_taste t on t.kind = f.kind and t.feature = f.feature
            join sc_recipe_norm rn on rn.recipe_id = f.recipe_id
            cross join sc_taste_norm tn
            group by f.recipe_id, tn.n, rn.n
        ),

        -- Which single feature carried it, so an explanation can name one.
        sc_content_top as (
            select distinct on (f.recipe_id, f.kind) f.recipe_id, f.kind, f.feature
            from sc_features f
            join sc_idf i on i.kind = f.kind and i.feature = f.feature
            join sc_taste t on t.kind = f.kind and t.feature = f.feature
            order by f.recipe_id, f.kind, (t.weight * i.idf) desc, f.feature
        ),

        -- Seasonality, learned and never curated: a curated table goes stale. Per tag rather than per recipe, since
        -- a few hundred entries a year make a per-recipe month distribution one observation.
        sc_tag_season as (
            select t.slug,
                   count(*) filter (where extract(month from c.made_at) = @scMonth::int)::numeric
                       / count(*)::numeric as share
            from cook_log_entries c
            join recipe_tags rt on rt.recipe_id = c.recipe_id
            join tags t on t.id = rt.tag_id
            where c.household_id = @householdId::uuid and c.made_at <= @scAsOf::timestamptz
            group by t.slug
            -- The evidence gate: below it there is no seasonal term or reason, rather than something confidently wrong.
            having count(*) >= @scSeasonMinObservations::bigint
        ),

        sc_season as (
            -- Zero at an even spread, one at three times it.
            select rt.recipe_id, max(least((ts.share * 12 - 1) / 2, 1)) as value
            from sc_tag_season ts
            join tags t on t.household_id = any(@library::uuid[]) and t.slug = ts.slug
            join recipe_tags rt on rt.tag_id = t.id
            where ts.share >= @scSeasonMinShare::numeric
            group by rt.recipe_id
        ),

        -- Meal type, which the plan has and recipes do not. Laplace smoothed toward "no opinion".
        sc_slot as (
            select m.recipe_id,
                   (count(*) filter (where m.slot = @scSlot::text) + 1.0) / (count(*) + 2.0) as p
            from meal_plan_entries m
            where m.household_id = @householdId::uuid
            group by m.recipe_id
        ),

        -- A proxy for how much of a project it is: used to rank, never shown.
        sc_effort as (
            select r.id as recipe_id,
                   least(
                       1.0,
                       (coalesce(
                            nullif(coalesce(r.prep_minutes, 0) + coalesce(r.cook_minutes, 0), 0),
                            30)::numeric / 120.0) * 0.7
                       + (least((select count(*) from steps s where s.recipe_id = r.id), 12)::numeric
                          / 12.0) * 0.3) as value
            from recipes r
            where r.household_id = any(@library::uuid[])
        ),

        -- The features of the one recipe on screen, for "more like this". Empty when nobody asked.
        sc_like_features as (
            select kind, feature from sc_features where recipe_id = @scLikeRecipeId::uuid
        ),

        -- Jaccard over tags and ingredients: content similarity, since co-occurrence in a few users' histories is mostly coincidence.
        sc_similarity as (
            select f.recipe_id,
                   count(*) filter (where lf.feature is not null)::numeric
                       / nullif(
                           (select count(*) from sc_like_features)
                           + count(*)
                           - count(*) filter (where lf.feature is not null),
                           0) as value
            from sc_features f
            left join sc_like_features lf on lf.kind = f.kind and lf.feature = f.feature
            where f.recipe_id is distinct from @scLikeRecipeId::uuid
            group by f.recipe_id
        ),

        -- "Not tonight" expires, so hiding something once does not become hiding it forever.
        sc_dismissed as (
            select recipe_id
            from suggestion_dismissals
            where user_id = @userId::uuid
              and dismissed_at > @scAsOf::timestamptz - make_interval(days => @scDismissalDays::int)
        ),

        sc_scores as (
            select
                r.id as recipe_id,

                @wAffinity::numeric * coalesce(a.value, 0) as affinity,

                @wContent::numeric * coalesce(c.value, 0) as content,

                -- Smooth, not a cliff: about -1 the same day, half after a week, nothing after two months. Weighted above
                -- affinity so the household favourite is net negative the day after and itself again a month later.
                -@wRepetition::numeric * case
                    when lc.at is null then 0
                    else exp(-(greatest(extract(epoch from (@scAsOf::timestamptz - lc.at)), 0) / 86400.0
                               / @scRepetitionDecay::numeric)::numeric)
                end as repetition,

                -- Only for something they liked: scaling by affinity surfaces loved-and-forgotten recipes, not tried-once ones.
                @wRediscovery::numeric * case
                    when coalesce(a.value, 0) <= 0 or lc.at is null then 0
                    else least(
                             greatest(
                                 (extract(epoch from (@scAsOf::timestamptz - lc.at)) / 86400.0
                                  - @scRediscoveryFrom::numeric)
                                 / nullif(@scRediscoveryFull::numeric - @scRediscoveryFrom::numeric, 0),
                                 0),
                             1)
                         * least(a.value, 1)
                end as rediscovery,

                case
                    when @scSlot::text is null then 0
                    else @wSlot::numeric * (coalesce(sl.p, 0.5) - 0.5) * 2
                end as slot,

                -- A Saturday tolerates a project; a Wednesday does not.
                case when @scIsWeekend::boolean then 0.2 else -1.0 end
                    * @wEffort::numeric * coalesce(ef.value, 0) as effort,

                @wHousehold::numeric * coalesce(h.value, 0) as household,

                case when lc.at is null then @wNovelty::numeric else 0 end as novelty,

                -- Reserved slots for new items: a household that imports two hundred recipes has two hundred with no history.
                @wFreshness::numeric * power(
                    0.5::numeric,
                    (greatest(extract(epoch from (@scAsOf::timestamptz - coalesce(o.imported_at, r.created_at))), 0)
                     / 86400.0 / @scFreshnessHalfLife::numeric)::numeric) as freshness,

                @wSeason::numeric * coalesce(se.value, 0) as season,

                @wSimilarity::numeric * coalesce(sim.value, 0) as similarity,

                -- Seeded by the day, so the list stays the same all evening across devices and refreshes and changes tomorrow.
                @wExploration::numeric
                    * (get_byte(decode(md5(@scSeed::text || r.id::text), 'hex'), 0)::numeric / 255.0 - 0.5)
                    as exploration,

                ct.feature as tag_subject,
                ci.feature as ingredient_subject,
                ht.display_name as household_subject,
                (sc_dismissed.recipe_id is not null) as dismissed

            from recipes r
            left join sc_affinity a on a.recipe_id = r.id
            left join sc_content c on c.recipe_id = r.id
            left join sc_household h on h.recipe_id = r.id
            left join sc_household_top ht on ht.recipe_id = r.id
            left join sc_last_cooked lc on lc.recipe_id = r.id
            left join sc_season se on se.recipe_id = r.id
            left join sc_slot sl on sl.recipe_id = r.id
            left join sc_effort ef on ef.recipe_id = r.id
            left join sc_similarity sim on sim.recipe_id = r.id
            left join recipe_origins o on o.recipe_id = r.id
            left join sc_content_top ct on ct.recipe_id = r.id and ct.kind = 'tag'
            left join sc_content_top ci on ci.recipe_id = r.id and ci.kind = 'ingredient'
            left join sc_dismissed on sc_dismissed.recipe_id = r.id
            where r.household_id = any(@library::uuid[])
        ),

        -- Rounded: exp() and power() over numeric return arbitrary precision that can overflow System.Decimal, and six
        -- places lets a cursor key round-trip exactly without returning one recipe on two pages.
        suggestion_scores as (
            select s.recipe_id,
                   round(s.affinity, 6) as affinity,
                   round(s.content, 6) as content,
                   round(s.repetition, 6) as repetition,
                   round(s.rediscovery, 6) as rediscovery,
                   round(s.slot, 6) as slot,
                   round(s.effort, 6) as effort,
                   round(s.household, 6) as household,
                   round(s.novelty, 6) as novelty,
                   round(s.freshness, 6) as freshness,
                   round(s.season, 6) as season,
                   round(s.similarity, 6) as similarity,
                   round(s.exploration, 6) as exploration,
                   s.tag_subject,
                   s.ingredient_subject,
                   s.household_subject,
                   s.dismissed,
                   round(
                       s.affinity + s.content + s.repetition + s.rediscovery + s.slot + s.effort
                       + s.household + s.novelty + s.freshness + s.season + s.similarity
                       + s.exploration,
                       6) as score
            from sc_scores s
        )
        """;

    /// <summary>The occasion and the weights, as parameters; not <c>householdId</c>, <c>library</c> or <c>userId</c>, which callers already pass.</summary>
    internal static DynamicParameters Parameters(SuggestionContext context, RankingWeights weights)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(weights);

        var asOf = context.AsOf.UtcDateTime;
        var parameters = new DynamicParameters();

        parameters.Add("scAsOf", asOf);
        parameters.Add("scMonth", asOf.Month);
        parameters.Add("scIsWeekend", asOf.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        // The plan's own encoding, reused so there is no second opinion on meal_plan_entries.slot.
        parameters.Add("scSlot", context.Slot is { } slot ? PlanningCodes.Of(slot) : null);
        parameters.Add("scLikeRecipeId", context.LikeRecipeId);
        parameters.Add("scSeed", Seed(context));

        parameters.Add("scHalfLife", weights.HalfLifeDays);
        parameters.Add("scRepetitionDecay", weights.RepetitionDecayDays);
        parameters.Add("scRediscoveryFrom", weights.RediscoveryFromDays);
        parameters.Add("scRediscoveryFull", weights.RediscoveryFullDays);
        parameters.Add("scFreshnessHalfLife", weights.FreshnessHalfLifeDays);
        parameters.Add("scDismissalDays", weights.DismissalDays);
        parameters.Add("scSeasonMinObservations", weights.SeasonMinimumObservations);
        parameters.Add("scSeasonMinShare", weights.SeasonMinimumShare);

        // "More like this" asks about the recipe on screen, not the person, so personal terms are turned down
        // rather than off: at full strength their favourites would outscore genuine resemblance.
        var personal = context.Purpose == SuggestionPurpose.Like ? 0.25m : 1m;

        parameters.Add("wAffinity", weights.Affinity * personal);
        parameters.Add("wContent", weights.Content);
        parameters.Add("wRepetition", weights.Repetition);
        parameters.Add("wRediscovery", weights.Rediscovery * personal);
        parameters.Add("wSlot", weights.Slot);
        parameters.Add("wEffort", weights.Effort);
        parameters.Add("wHousehold", weights.Household * personal);
        parameters.Add("wNovelty", weights.Novelty * personal);
        parameters.Add("wFreshness", weights.Freshness * personal);
        parameters.Add("wSeason", weights.Season);

        // Similarity only carries weight when somebody actually asked "more like this".
        parameters.Add(
            "wSimilarity",
            context.Purpose == SuggestionPurpose.Like ? weights.Similarity : 0m);

        // Never while paging: a cursor resumes one order.
        parameters.Add(
            "wExploration",
            context.Purpose == SuggestionPurpose.Browse ? 0m : weights.Exploration);

        return parameters;
    }

    // Seeded by person, day and question, not the clock, so the answer settles for the evening.
    private static string Seed(SuggestionContext context) =>
        $"{context.UserId:N}:{context.AsOf.UtcDateTime:yyyyMMdd}:{(int)context.Purpose}:";
}
