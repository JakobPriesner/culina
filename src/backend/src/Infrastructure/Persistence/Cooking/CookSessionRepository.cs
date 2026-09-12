using Application.Abstractions;
using Domain.Cooking;
using Domain.Shared;

namespace Infrastructure.Persistence.Cooking;

/// <summary>The <c>cook_sessions</c> row.</summary>
internal sealed record CookSessionRow
{
    public Guid Id { get; init; }

    public Guid RecipeId { get; init; }

    public Guid UserId { get; init; }

    public Guid HouseholdId { get; init; }

    public decimal Servings { get; init; }

    public int CurrentStepIndex { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset LastActiveAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public DateTimeOffset? AbandonedAt { get; init; }

    public long Version { get; init; }

    internal CookSession ToSession() => CookSession.Rehydrate(
        Id,
        RecipeId,
        UserId,
        HouseholdId,
        Servings,
        CurrentStepIndex,
        StartedAt,
        LastActiveAt,
        CompletedAt,
        AbandonedAt,
        Version);
}

/// <summary>Cooking sessions, in PostgreSQL.</summary>
internal sealed class CookSessionRepository(DbExecutor executor) : ICookSessionRepository
{
    private const string Columns =
        "id, recipe_id, user_id, household_id, servings, current_step_index, "
        + "started_at, last_active_at, completed_at, abandoned_at, version";

    public async Task<CookSession?> ActiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<CookSessionRow>(
            $"""
             select {Columns}
             from cook_sessions
             where user_id = @userId and completed_at is null and abandoned_at is null;
             """,
            new { userId },
            cancellationToken).ConfigureAwait(false);

        return row?.ToSession();
    }

    public async Task<Result<CookSession>> FindAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<CookSessionRow>(
            $"""
             select {Columns}
             from cook_sessions
             where id = @sessionId and user_id = @userId;
             """,
            new { sessionId, userId },
            cancellationToken).ConfigureAwait(false);

        // Not found and not yours are the same answer: otherwise the difference
        // tells a stranger that a session with this id exists.
        return row is null ? CookingErrors.SessionNotFound : row.ToSession();
    }

    public async Task<Result> StartAsync(
        CookSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Both statements or neither. The partial unique index refuses a second
        // active session, so the abandonment is not a courtesy — it is what
        // makes the insert legal.
        await executor.ExecuteAsync(
            """
            update cook_sessions
            set abandoned_at = @now, last_active_at = @now, version = version + 1
            where user_id = @userId and completed_at is null and abandoned_at is null;
            """,
            new { userId = session.UserId, now },
            cancellationToken).ConfigureAwait(false);

        await executor.ExecuteAsync(
            """
            insert into cook_sessions
                (id, recipe_id, user_id, household_id, servings, current_step_index,
                 started_at, last_active_at, version)
            values
                (@id, @recipeId, @userId, @householdId, @servings, @currentStepIndex,
                 @startedAt, @lastActiveAt, @version);
            """,
            new
            {
                id = session.Id,
                recipeId = session.RecipeId,
                userId = session.UserId,
                householdId = session.HouseholdId,
                servings = session.Servings,
                currentStepIndex = session.CurrentStepIndex,
                startedAt = session.StartedAt,
                lastActiveAt = session.LastActiveAt,
                version = session.Version
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> TouchAsync(CookSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Two columns, no version check. This runs on every step advance, and
        // making each one a concurrency event would leave a second device
        // permanently stale for no benefit.
        var changed = await executor.ExecuteAsync(
            """
            update cook_sessions
            set current_step_index = @currentStepIndex, last_active_at = @lastActiveAt
            where id = @id and completed_at is null and abandoned_at is null;
            """,
            new
            {
                id = session.Id,
                currentStepIndex = session.CurrentStepIndex,
                lastActiveAt = session.LastActiveAt
            },
            cancellationToken).ConfigureAwait(false);

        return changed == 0 ? CookingErrors.SessionFinished : Result.Success();
    }

    public async Task<Result> UpdateAsync(
        CookSession session,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        // The version is in the WHERE, so zero rows means somebody else changed
        // it first — never a read-then-write race.
        var changed = await executor.ExecuteAsync(
            """
            update cook_sessions
            set servings = @servings,
                current_step_index = @currentStepIndex,
                last_active_at = @lastActiveAt,
                completed_at = @completedAt,
                abandoned_at = @abandonedAt,
                version = @version
            where id = @id and version = @expectedVersion;
            """,
            new
            {
                id = session.Id,
                servings = session.Servings,
                currentStepIndex = session.CurrentStepIndex,
                lastActiveAt = session.LastActiveAt,
                completedAt = session.CompletedAt,
                abandonedAt = session.AbandonedAt,
                version = session.Version,
                expectedVersion
            },
            cancellationToken).ConfigureAwait(false);

        return changed == 0 ? ConcurrencyErrors.VersionMismatch : Result.Success();
    }
}
