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
/// Asks the ranker what it would have suggested just before each thing a
/// household actually cooked. See <c>docs/suggestions-research.md</c> §M.1.
/// </summary>
/// <remarks>
/// <para>
/// The cook log is dated, so the counterfactual is computable without shipping
/// anything: for an entry at <c>t</c>, put the household back the way it was
/// just before <c>t</c>, ask for a shortlist, and see whether what they cooked
/// is on it. A few hundred entries is nowhere near enough to fit a weight
/// vector, and plenty to say which of two is better.
/// </para>
/// <para>
/// <b>The world is put back by deleting the future, and then not keeping the
/// deletion.</b> Everything dated at or after <c>t</c> is removed inside a
/// transaction, the real <see cref="SuggestionRanker"/> runs inside the same
/// transaction, and the transaction is rolled back. So the ranking under test
/// is the production SQL, unaltered — a replay with its own copy of the scoring
/// would measure the copy.
/// </para>
/// <para>
/// What cannot be put back is what was edited in place: a recipe's current
/// tags and ingredients stand in for whatever they were at <c>t</c>, and a plan
/// entry has no creation date, so everything planned for <c>t</c>'s day or later
/// is treated as not yet planned. The second errs towards knowing too little,
/// which is the safe direction: planning dinner and then cooking it would
/// otherwise hand the ranker the answer.
/// </para>
/// </remarks>
/// <param name="session">
/// One connection for the whole run. Nothing it does survives: every point is a
/// transaction that is rolled back.
/// </param>
/// <param name="householdId">Whose cook log.</param>
internal sealed class SuggestionReplay(DbSession session, Guid householdId)
{
    /// <summary>How long a household's history runs before its entries are worth predicting.</summary>
    /// <remarks>
    /// The first weeks of a cook log are a cold start, and every ranker is bad
    /// at them in the same way. Scoring them would mostly measure the length of
    /// the history rather than the quality of the weights.
    /// </remarks>
    internal const int WarmUpDays = 90;

    /// <summary>The longest shortlist scored. recall@5 is read off the front of it.</summary>
    internal const int ListLength = 10;

    private readonly DbExecutor executor = new(session);

    /// <summary>Every entry worth predicting, oldest first.</summary>
    /// <remarks>
    /// An entry for a recipe written down after it was cooked — somebody logging
    /// last week's dinner the day they imported it — is left out: the ranker
    /// cannot be blamed for not suggesting a recipe that was not in the book.
    /// </remarks>
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

    /// <summary>What the ranker, weighted like this, would have suggested before each point.</summary>
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
                // Ending without a commit is the rollback, and it is the whole
                // trick: the future is deleted for exactly one question.
                await session.EndTransactionAsync();
            }
        }

        return outcomes;
    }

    /// <summary>Deletes everything the household did at or after <paramref name="at"/>, and says how many recipes are left.</summary>
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

    /// <summary>
    /// The question the front page would have asked that day: a shortlist for
    /// whoever cooked, as of that morning.
    /// </summary>
    /// <remarks>
    /// Truncated to the day exactly as the endpoint truncates it, so the replay
    /// asks the question people are actually asked, jitter and all.
    /// </remarks>
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
