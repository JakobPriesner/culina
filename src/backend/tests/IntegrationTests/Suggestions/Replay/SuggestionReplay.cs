using Application.Abstractions;
using Domain.Suggestions;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Suggestions;

namespace IntegrationTests.Suggestions.Replay;

/// <summary>One cook-log entry to predict: who cooked what, and when.</summary>
internal sealed record ReplayPoint
{
    public Guid UserId { get; init; }

    public Guid RecipeId { get; init; }

    public DateTimeOffset MadeAt { get; init; }
}

/// <summary>What the ranker would have suggested just before a point, and how big the library was then.</summary>
internal sealed record ReplayOutcome(ReplayPoint Point, IReadOnlyList<ScoredRecipe> Suggested, int LibrarySize)
{
    /// <summary>Where the recipe actually cooked came in the list, counting from one, or null when it was not in it.</summary>
    public int? Rank
    {
        get
        {
            for (var index = 0; index < Suggested.Count; index++)
            {
                if (Suggested[index].Recipe.RecipeId == Point.RecipeId)
                {
                    return index + 1;
                }
            }

            return null;
        }
    }
}

/// <summary>
/// Asks the ranker what it would have suggested just before each thing a household cooked; see <c>docs/suggestions-research.md</c> §M.1.
/// </summary>
/// <remarks>
/// The future is deleted inside a transaction, the production <see cref="SuggestionRanker"/> runs, and the transaction is rolled back,
/// so the SQL under test is unaltered. Edits in place cannot be rewound (current tags stand in), and plan entries from that day on count as
/// not yet planned: knowing too little is the safe direction.
/// </remarks>
/// <param name="session">One connection for the whole run; every point is a rolled-back transaction.</param>
/// <param name="householdId">Whose cook log.</param>
/// <param name="householdId">Whose cook log.</param>
internal sealed class SuggestionReplay(DbSession session, Guid householdId)
{
    // How long a history runs before its entries are worth predicting: the first weeks are a cold start that would measure history length, not weights.
    internal const int WarmUpDays = 90;

    // The longest shortlist scored. recall@5 is read off the front of it.
    internal const int ListLength = 10;

    private readonly DbExecutor executor = new(session);

    // Every entry worth predicting, oldest first. Entries for recipes written down after they were cooked are left out: not in the book yet.
    internal Task<IReadOnlyList<ReplayPoint>> PointsAsync(CancellationToken cancellationToken) =>
        executor.QueryAsync<ReplayPoint>(
            """
            select c.user_id, c.recipe_id, c.made_at
            from cook_log_entries c
            join recipes r on r.id = c.recipe_id
            where c.household_id = @householdId
              and r.created_at < c.made_at
              and c.made_at >= (
                    select min(made_at) from cook_log_entries where household_id = @householdId
                  ) + make_interval(days => @warmUpDays)
            order by c.made_at, c.id;
            """,
            new { householdId, warmUpDays = WarmUpDays },
            cancellationToken);

    // What the ranker, weighted like this, would have suggested before each point.
    internal async Task<IReadOnlyList<ReplayOutcome>> RunAsync(
        IReadOnlyList<ReplayPoint> points,
        RankingWeights weights,
        CancellationToken cancellationToken)
    {
        var ranker = new SuggestionRanker(new SuggestionReader(executor, weights), weights);
        List<ReplayOutcome> outcomes = [];

        foreach (var point in points)
        {
            await session.BeginTransactionAsync(cancellationToken);

            try
            {
                var librarySize = await RewindAsync(point.MadeAt, cancellationToken);
                var suggested = await ranker.RankAsync(ContextFor(point), cancellationToken);

                outcomes.Add(new ReplayOutcome(point, suggested, librarySize));
            }
            finally
            {
                // No commit is the rollback: the future is deleted for exactly one question.
                await session.EndTransactionAsync();
            }
        }

        return outcomes;
    }

    // Deletes everything the household did at or after `at`, and says how many recipes are left.
    private async Task<int> RewindAsync(DateTimeOffset at, CancellationToken cancellationToken)
    {
        await executor.ExecuteAsync(
            """
            delete from recipes where household_id = @householdId and created_at >= @at;

            delete from cook_log_entries where household_id = @householdId and made_at >= @at;

            delete from meal_plan_entries where household_id = @householdId and on_date >= @at::date;

            delete from cookbook_recipes cr
            using cookbooks cb
            where cb.id = cr.cookbook_id and cb.household_id = @householdId and cr.added_at >= @at;

            delete from personal_notes n
            using recipes r
            where r.id = n.recipe_id and r.household_id = @householdId and n.updated_at >= @at;

            delete from cook_sessions
            where household_id = @householdId
              and coalesce(completed_at, abandoned_at, started_at) >= @at;

            delete from suggestion_dismissals d
            using recipes r
            where r.id = d.recipe_id and r.household_id = @householdId and d.dismissed_at >= @at;
            """,
            new { householdId, at },
            cancellationToken);

        return await executor.ExecuteScalarAsync<int>(
            "select count(*)::int from recipes where household_id = @householdId;",
            new { householdId },
            cancellationToken);
    }

    // The question the front page would have asked that day, truncated to the day as the endpoint does it.
    private SuggestionContext ContextFor(ReplayPoint point) => new(
        householdId,
        point.UserId,
        SuggestionPurpose.Decide,
        new DateTimeOffset(point.MadeAt.UtcDateTime.Date, TimeSpan.Zero),
        Slot: null,
        MaxMinutes: null,
        Tags: [],
        Ingredients: [],
        LikeRecipeId: null,
        Exclude: [],
        Count: ListLength);
}
