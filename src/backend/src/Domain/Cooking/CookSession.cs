using Domain.Shared;

namespace Domain.Cooking;

/// <summary>
/// Somebody is cooking something, right now.
/// </summary>
/// <remarks>
/// <para>
/// Person-owned, and at most one is active per person: the phone propped
/// against the mixing bowl and the laptop on the counter are the same cook, and
/// "where was I" has to have one answer.
/// </para>
/// <para>
/// It holds the scaling in force, because coming back to a recipe you had
/// scaled to six and finding it at four is worse than not remembering at all.
/// </para>
/// <para>
/// <strong>Timers are not here.</strong> A timer is device-bound and must keep
/// ticking while the app is closed, so it lives in the browser keyed by session
/// id. A cooking session is a fact worth persisting; a timer is local ephemera.
/// </para>
/// </remarks>
public sealed class CookSession
{
    private CookSession(
        Guid id,
        Guid recipeId,
        Guid userId,
        Guid householdId,
        decimal servings,
        int currentStepIndex,
        DateTimeOffset startedAt,
        DateTimeOffset lastActiveAt,
        DateTimeOffset? completedAt,
        DateTimeOffset? abandonedAt,
        long version)
    {
        Id = id;
        RecipeId = recipeId;
        UserId = userId;
        HouseholdId = householdId;
        Servings = servings;
        CurrentStepIndex = currentStepIndex;
        StartedAt = startedAt;
        LastActiveAt = lastActiveAt;
        CompletedAt = completedAt;
        AbandonedAt = abandonedAt;
        Version = version;
    }

    /// <summary>The session's id.</summary>
    public Guid Id { get; }

    /// <summary>What is being cooked.</summary>
    public Guid RecipeId { get; }

    /// <summary>Who is cooking it.</summary>
    public Guid UserId { get; }

    /// <summary>Which household the recipe belonged to when it started.</summary>
    public Guid HouseholdId { get; }

    /// <summary>The scaling in force.</summary>
    public decimal Servings { get; private set; }

    /// <summary>Which step they are on, from zero.</summary>
    public int CurrentStepIndex { get; private set; }

    /// <summary>When they started.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>When they last did anything. Drives resume and tidying up.</summary>
    public DateTimeOffset LastActiveAt { get; private set; }

    /// <summary>When they finished, if they did.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>When it was given up on, if it was.</summary>
    public DateTimeOffset? AbandonedAt { get; private set; }

    /// <summary>The entity version.</summary>
    public long Version { get; private set; }

    /// <summary>Whether this is the session a "now cooking" bar would show.</summary>
    public bool IsActive => CompletedAt is null && AbandonedAt is null;

    /// <summary>Starts cooking.</summary>
    /// <param name="recipeId">What is being cooked.</param>
    /// <param name="userId">Who is cooking it.</param>
    /// <param name="householdId">Which household owns the recipe.</param>
    /// <param name="servings">The scaling to cook at.</param>
    /// <param name="now">The injected clock's reading.</param>
    public static Result<CookSession> Start(
        Guid recipeId,
        Guid userId,
        Guid householdId,
        decimal servings,
        DateTimeOffset now) =>
        EnsureServings(servings).Map(() => new CookSession(
            CulinaId.New(),
            recipeId,
            userId,
            householdId,
            servings,
            currentStepIndex: 0,
            startedAt: now,
            lastActiveAt: now,
            completedAt: null,
            abandonedAt: null,
            version: 1));

    /// <summary>Rebuilds one that was stored.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="recipeId">What is being cooked.</param>
    /// <param name="userId">Who is cooking it.</param>
    /// <param name="householdId">Which household owns the recipe.</param>
    /// <param name="servings">The scaling in force.</param>
    /// <param name="currentStepIndex">Which step they are on.</param>
    /// <param name="startedAt">When they started.</param>
    /// <param name="lastActiveAt">When they last did anything.</param>
    /// <param name="completedAt">When they finished, if they did.</param>
    /// <param name="abandonedAt">When it was given up on, if it was.</param>
    /// <param name="version">The stored version.</param>
    public static CookSession Rehydrate(
        Guid id,
        Guid recipeId,
        Guid userId,
        Guid householdId,
        decimal servings,
        int currentStepIndex,
        DateTimeOffset startedAt,
        DateTimeOffset lastActiveAt,
        DateTimeOffset? completedAt,
        DateTimeOffset? abandonedAt,
        long version) =>
        new(
            id,
            recipeId,
            userId,
            householdId,
            servings,
            currentStepIndex,
            startedAt,
            lastActiveAt,
            completedAt,
            abandonedAt,
            version);

    /// <summary>Moves to a step.</summary>
    /// <param name="index">Which step, from zero.</param>
    /// <param name="now">The injected clock's reading.</param>
    /// <remarks>
    /// Deliberately does not bump the version. This happens on every step, and
    /// making each advance a concurrency event would mean a second device is
    /// permanently stale for no benefit — the last tap genuinely is the truth
    /// about where the cook is.
    /// </remarks>
    public Result MoveTo(int index, DateTimeOffset now)
    {
        if (!IsActive)
        {
            return CookingErrors.SessionFinished;
        }

        if (index < 0)
        {
            return CookingErrors.StepOutOfRange;
        }

        CurrentStepIndex = index;
        LastActiveAt = now;

        return Result.Success();
    }

    /// <summary>Changes the scaling mid-cook, because one more person arrived.</summary>
    /// <param name="servings">The new scaling.</param>
    /// <param name="now">The injected clock's reading.</param>
    public Result Rescale(decimal servings, DateTimeOffset now) =>
        !IsActive
            ? CookingErrors.SessionFinished
            : EnsureServings(servings).Tap(() =>
            {
                Servings = servings;
                LastActiveAt = now;
                Version += 1;
            });

    /// <summary>Finishes.</summary>
    /// <param name="now">The injected clock's reading.</param>
    public Result Complete(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return CookingErrors.SessionFinished;
        }

        CompletedAt = now;
        LastActiveAt = now;
        Version += 1;

        return Result.Success();
    }

    /// <summary>
    /// Gives up on it.
    /// </summary>
    /// <param name="now">The injected clock's reading.</param>
    /// <remarks>
    /// Abandoning an already-finished session is not an error: starting a new
    /// session abandons whatever was active, and a race between two devices
    /// doing that must not fail the one that arrives second.
    /// </remarks>
    public void Abandon(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        AbandonedAt = now;
        LastActiveAt = now;
        Version += 1;
    }

    private static Result EnsureServings(decimal servings) =>
        servings is > 0 and <= 1000 ? Result.Success() : CookingErrors.InvalidServings;
}
