using Application.Abstractions;
using Dapper;
using Domain.Suggestions;
using Infrastructure.Persistence.Planning;

namespace Infrastructure.Persistence.Suggestions;

/// <summary>
/// The arithmetic behind "what should I cook?", as one block of common table
/// expressions ending in <c>suggestion_scores</c>.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from the two queries that use it — the recipe search, when it is
/// asked for <c>sort=suggested</c>, and the suggestion ranker — for the same
/// reason <see cref="Recipes.RecipeSearchSql"/> is kept apart from the searcher:
/// the ordering is a thing you read on its own, and two copies of ninety lines
/// of scoring would be two rankings that quietly disagree about the same recipe.
/// </para>
/// <para>
/// <b>Every term is a score and no term is a threshold.</b> A recipe the
/// household ate yesterday sinks to the bottom; it is never removed. That is
/// what makes the ranker's guarantee — it returns everything eligible, ranked —
/// hold on a saturated household as well as on an empty one.
/// </para>
/// <para>
/// <b>Nothing is materialised.</b> The scores are worked out whenever somebody
/// looks, which is the same decision smart cookbooks made: a recipe written this
/// evening is ranked correctly this evening, there is no job to run, nothing to
/// backfill when a weight changes, and no stored ranking that can drift out of
/// step with the recipes it claims to describe. For a household's few hundred
/// recipes and few hundred cook-log entries this is a few milliseconds;
/// <c>culina.usecase.duration</c> is what says when that stops being true.
/// </para>
/// </remarks>
internal static class SuggestionScoringSql
{
    /// <summary>
    /// The CTE block, without the leading <c>with</c> and with a trailing comma
    /// so a caller can append its own.
    /// </summary>
    /// <remarks>
    /// Reads <c>@householdId</c>, <c>@library</c> and <c>@userId</c>, which
    /// both callers already pass, plus the <c>sc*</c> and <c>w*</c> parameters from
    /// <see cref="Parameters"/>.
    /// </remarks>
    internal const string Ctes = """
        -- Every interaction that says something, in one shape, so that
        -- weighting and decay are expressed once instead of per table.
        sc_signals as (
            -- Cooking it is the unit every other weight is measured against.
            -- Photographing the result is rarer and means more: nobody takes a
            -- picture of a dinner that disappointed them.
            select c.recipe_id,
                   c.user_id,
                   (case when c.image_hash is null then 1.0 else 1.3 end)::numeric as weight,
                   c.made_at as at,
                   true as decays
            from cook_log_entries c
            where c.household_id = @householdId::uuid

            union all

            -- Planning it is intent rather than completion, and it belongs to
            -- the household rather than to whoever typed it. A null user is
            -- what lets it feed household popularity and the slot prior without
            -- becoming one person's personal taste.
            select m.recipe_id, null::uuid, 0.6, m.on_date::timestamptz, true
            from meal_plan_entries m
            where m.household_id = @householdId::uuid

            union all

            -- A shelf is a standing statement, not an event, so it does not
            -- decay. Decaying it would be saying the curation expired.
            select cr.recipe_id, cr.added_by, 0.8, cr.added_at, false
            from cookbook_recipes cr
            join cookbooks cb on cb.id = cr.cookbook_id
            where cb.household_id = @householdId::uuid

            union all

            -- Nobody annotates a recipe they are indifferent to. On an
            -- inherited recipe only this household's own people count: the
            -- other kitchen's notes are that kitchen's history.
            select n.recipe_id, n.user_id, 0.4, n.updated_at, true
            from personal_notes n
            join recipes nr on nr.id = n.recipe_id
            where nr.household_id = @householdId::uuid
               or (nr.household_id = any(@library::uuid[])
                   and n.user_id in (
                       select hm.user_id from household_members hm
                       where hm.household_id = @householdId::uuid))

            union all

            -- Finishing what you started.
            --
            -- Abandonment is deliberately absent. Starting a session abandons
            -- the previous one — a partial unique index enforces exactly one
            -- active session per person — so an abandoned session is
            -- overwhelmingly a consequence of cooking something else, not a
            -- judgement. Reading it as dislike would systematically punish the
            -- recipes people cook most often, because those are the ones they
            -- start again.
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
            -- greatest(..., 0) rather than a filter: a meal planned for Thursday
            -- has not happened, but it is still a statement of intent, and
            -- without the floor its "negative age" would make it worth more than
            -- something somebody actually cooked.
        ),

        -- What this person has done with each recipe. log1p, because the
        -- difference between cooked once and cooked twice is large and the
        -- difference between eleven and twelve is nothing — and linear
        -- weighting lets one weekly staple own every list forever.
        sc_affinity as (
            select recipe_id, ln(1 + sum(w)) as value
            from sc_weighted
            where user_id = @userId::uuid
            group by recipe_id
        ),

        -- What everybody else has. The honest residue of collaborative
        -- filtering at two to eight users: not a model, just "your partner
        -- cooks this".
        sc_household as (
            select recipe_id, ln(1 + sum(w)) as value
            from sc_weighted
            where user_id is distinct from @userId::uuid
            group by recipe_id
        ),

        -- Who, so the reason can say a name instead of "somebody".
        sc_household_top as (
            select distinct on (c.recipe_id) c.recipe_id, u.display_name
            from cook_log_entries c
            join users u on u.id = c.user_id
            where c.household_id = @householdId::uuid and c.user_id <> @userId::uuid
            group by c.recipe_id, u.display_name
            order by c.recipe_id, count(*) desc, u.display_name
        ),

        -- When anyone in the household last made it. Household-wide on purpose:
        -- if your partner made the lasagne on Tuesday then you ate it, and it
        -- should not be suggested to you on Wednesday even though your own cook
        -- log is silent. No general-purpose recommender expresses this, and it
        -- is the most product-specific line in the file.
        sc_last_cooked as (
            select recipe_id, max(made_at) as at
            from cook_log_entries
            where household_id = @householdId::uuid and made_at <= @scAsOf::timestamptz
            group by recipe_id
        ),

        -- The only content features Culina has: household-authored tags and the
        -- ingredient names somebody typed. That is enough precisely because the
        -- model is household-local too — "vegetarisch" does not have to mean
        -- anything outside this kitchen.
        sc_features as (
            select r.id as recipe_id, 'tag' as kind, t.slug as feature
            from recipes r
            join recipe_tags rt on rt.recipe_id = r.id
            join tags t on t.id = rt.tag_id
            where r.household_id = any(@library::uuid[])

            union

            -- Folded the way the shopping list folds a name, and no further:
            -- stemming or a synonym table would be a third ingredient
            -- vocabulary to keep in step with the two that already exist.
            select r.id, 'ingredient', lower(unaccent(btrim(i.name)))
            from recipes r
            join ingredient_groups g on g.recipe_id = r.id
            join recipe_ingredients i on i.group_id = g.id
            where r.household_id = any(@library::uuid[]) and btrim(i.name) <> ''
        ),

        -- Inverse document frequency, and it matters more here than anywhere
        -- else: in a kitchen where nine recipes in ten contain salt, salt has to
        -- carry no information at all. A feature on every recipe scores exactly
        -- zero.
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

        -- Cosine between the two. This is the term that carries a recipe nobody
        -- has ever touched, which is why the app is useful in week one instead
        -- of in year four.
        sc_content as (
            select f.recipe_id, sum(t.weight * i.idf) / nullif(tn.n * rn.n, 0) as value
            from sc_features f
            join sc_idf i on i.kind = f.kind and i.feature = f.feature
            join sc_taste t on t.kind = f.kind and t.feature = f.feature
            join sc_recipe_norm rn on rn.recipe_id = f.recipe_id
            cross join sc_taste_norm tn
            group by f.recipe_id, tn.n, rn.n
        ),

        -- Which single feature carried it, so an explanation can name one
        -- rather than describe a vector.
        sc_content_top as (
            select distinct on (f.recipe_id, f.kind) f.recipe_id, f.kind, f.feature
            from sc_features f
            join sc_idf i on i.kind = f.kind and i.feature = f.feature
            join sc_taste t on t.kind = f.kind and t.feature = f.feature
            order by f.recipe_id, f.kind, (t.weight * i.idf) desc, f.feature
        ),

        -- Seasonality, learned and never curated.
        --
        -- A curated ingredient-to-season table was rejected for this project
        -- for the same reason a pantry inventory was: nobody maintains it, so
        -- it goes stale and poisons what is built on it. This asks the
        -- household's own cook log instead, per tag rather than per recipe,
        -- because at a few hundred entries a year a per-recipe month
        -- distribution is one observation and a hope, while every soup entry
        -- informs "soup".
        sc_tag_season as (
            select t.slug,
                   count(*) filter (where extract(month from c.made_at) = @scMonth::int)::numeric
                       / count(*)::numeric as share
            from cook_log_entries c
            join recipe_tags rt on rt.recipe_id = c.recipe_id
            join tags t on t.id = rt.tag_id
            where c.household_id = @householdId::uuid and c.made_at <= @scAsOf::timestamptz
            group by t.slug
            -- The evidence gate. Below it there is no seasonal term and no
            -- seasonal reason, so a fresh installation says nothing about
            -- seasons rather than something confident and wrong.
            having count(*) >= @scSeasonMinObservations::bigint
        ),

        sc_season as (
            -- Zero at an even spread, one at three times it. A claim about this
            -- kitchen, which the people in it can check.
            select rt.recipe_id, max(least((ts.share * 12 - 1) / 2, 1)) as value
            from sc_tag_season ts
            join tags t on t.household_id = any(@library::uuid[]) and t.slug = ts.slug
            join recipe_tags rt on rt.tag_id = t.id
            where ts.share >= @scSeasonMinShare::numeric
            group by rt.recipe_id
        ),

        -- Meal type, which recipes do not have and the plan does. Laplace
        -- smoothed toward "no opinion", so one stray breakfast entry does not
        -- turn a curry into a breakfast recipe.
        sc_slot as (
            select m.recipe_id,
                   (count(*) filter (where m.slot = @scSlot::text) + 1.0) / (count(*) + 2.0) as p
            from meal_plan_entries m
            where m.household_id = @householdId::uuid
            group by m.recipe_id
        ),

        -- A proxy for how much of a project it is, used to rank and never
        -- shown. A displayed difficulty score would be a number invented about
        -- somebody's cooking, which is a different thing from ordering two
        -- recipes by how long they take.
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

        -- The features of the one recipe somebody is looking at, for "more like
        -- this". Empty, and therefore harmless, when nobody asked.
        sc_like_features as (
            select kind, feature from sc_features where recipe_id = @scLikeRecipeId::uuid
        ),

        -- Jaccard over tags and ingredients. Content similarity rather than
        -- co-occurrence in user histories: with this many people the modal
        -- co-occurrence between two recipes is zero, and the non-zero ones are
        -- one person, one month and one coincidence. "Uses eleven of the same
        -- twelve ingredients" has content, and can be explained.
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

        -- "Not tonight", which expires, so that hiding something once does not
        -- quietly become hiding it forever.
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

                -- Smooth, not a cliff: roughly -1 the same day, half that after
                -- a week, a quarter after a fortnight, nothing after two months.
                -- Weighted above affinity on purpose, so that the household's
                -- favourite is net negative the day after it was made and
                -- itself again a month later.
                -@wRepetition::numeric * case
                    when lc.at is null then 0
                    else exp(-(greatest(extract(epoch from (@scAsOf::timestamptz - lc.at)), 0) / 86400.0
                               / @scRepetitionDecay::numeric)::numeric)
                end as repetition,

                -- Only for something they actually liked: you cannot
                -- rediscover a recipe you never made, and scaling by affinity
                -- is what surfaces the ones that were loved and forgotten
                -- rather than the ones tried once and abandoned.
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

                -- Culina's version of reserving slots for new items. A
                -- household that imports two hundred recipes has two hundred
                -- with no history at all, and a ranking made only of relevance
                -- would never show a single one of them.
                @wFreshness::numeric * power(
                    0.5::numeric,
                    (greatest(extract(epoch from (@scAsOf::timestamptz - coalesce(o.imported_at, r.created_at))), 0)
                     / 86400.0 / @scFreshnessHalfLife::numeric)::numeric) as freshness,

                @wSeason::numeric * coalesce(se.value, 0) as season,

                @wSimilarity::numeric * coalesce(sim.value, 0) as similarity,

                -- Deterministic, and seeded by the day rather than by chance.
                -- That is the whole point: the list is the same all evening, on
                -- both devices and after a refresh, and different tomorrow. A
                -- list that reshuffles under your thumb is worse than a
                -- slightly worse list that stays put, and a shuffle button
                -- would teach people the first answer was arbitrary.
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

        -- Rounded, and not as a tidiness measure.
        --
        -- exp() and power() over numeric return arbitrary precision: a recipe
        -- nobody has cooked for three years gives a repetition penalty with
        -- several hundred decimal places, and summing twelve of those produces a
        -- value that is perfectly ordinary in magnitude and does not fit in a
        -- System.Decimal at all. Six places is far past anything that could
        -- change an order, and it is also what lets a cursor key round-trip
        -- exactly: a page boundary that re-serialised to a different number
        -- would return one recipe on two pages.
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

    /// <summary>
    /// The occasion and the weights, as parameters.
    /// </summary>
    /// <remarks>
    /// Does <b>not</b> include <c>householdId</c>, <c>library</c> or
    /// <c>userId</c>: both callers already pass those for their own filtering, and a second copy under a
    /// different name is two values that can disagree.
    /// </remarks>
    /// <param name="context">The occasion.</param>
    /// <param name="weights">What each term is worth.</param>
    internal static DynamicParameters Parameters(SuggestionContext context, RankingWeights weights)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(weights);

        var asOf = context.AsOf.UtcDateTime;
        var parameters = new DynamicParameters();

        parameters.Add("scAsOf", asOf);
        parameters.Add("scMonth", asOf.Month);
        parameters.Add("scIsWeekend", asOf.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        // The plan's own encoding, reused rather than respelled: a scoring query
        // with its own opinion about what meal_plan_entries.slot contains is a
        // second opinion waiting to disagree.
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

        // "More like this" is a question about the recipe on screen, not about
        // the person asking — so everything that is about them is turned down
        // rather than off. Left at full strength, a recipe they cook often
        // outscores one that genuinely resembles what they are reading, and the
        // strip fills with their favourites under a heading that promises
        // something else. Taste still breaks ties; it no longer decides.
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

        // "More like this" is a different question from "what should I cook",
        // so similarity only carries weight when somebody actually asked it.
        parameters.Add(
            "wSimilarity",
            context.Purpose == SuggestionPurpose.Like ? weights.Similarity : 0m);

        // Never while paging: a cursor resumes an order, and an order that is
        // jittered per request is not one order.
        parameters.Add(
            "wExploration",
            context.Purpose == SuggestionPurpose.Browse ? 0m : weights.Exploration);

        return parameters;
    }

    /// <summary>
    /// What the exploration jitter is seeded with: this person, this day, this
    /// question. Not the clock, so the answer settles for the evening.
    /// </summary>
    private static string Seed(SuggestionContext context) =>
        $"{context.UserId:N}:{context.AsOf.UtcDateTime:yyyyMMdd}:{(int)context.Purpose}:";
}
