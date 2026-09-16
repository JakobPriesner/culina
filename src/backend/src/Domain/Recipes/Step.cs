using Domain.Shared;

namespace Domain.Recipes;

/// <summary>
/// One instruction.
/// </summary>
/// <remarks>
/// The text is held as segments rather than as a string, because a step
/// mentioning an ingredient has to render that ingredient's <em>scaled</em>
/// amount. See <see cref="StepText"/>.
/// </remarks>
public sealed class Step
{
    /// <summary>The longest step text the database column accepts.</summary>
    public const int MaxTextLength = 4000;

    /// <summary>The longest timer a step may start: one day.</summary>
    public const int MaxDurationSeconds = 86_400;

    private Step(
        Guid id,
        int sortOrder,
        IReadOnlyList<StepSegment> segments,
        IReadOnlySet<Guid> uses,
        int? durationSeconds)
    {
        Id = id;
        SortOrder = sortOrder;
        Segments = segments;
        Uses = uses;
        DurationSeconds = durationSeconds;
    }

    /// <summary>The step's id.</summary>
    public Guid Id { get; }

    /// <summary>Where it appears.</summary>
    public int SortOrder { get; }

    /// <summary>Its text, split into words and ingredient references.</summary>
    public IReadOnlyList<StepSegment> Segments { get; }

    /// <summary>
    /// Everything the step needs, whether or not the sentence names it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A step that says "combine everything and knead" needs five ingredients
    /// and names none, so the words alone cannot be the answer to "what do I
    /// get out for this step". This is the answer, and the mentions in the
    /// sentence are a guaranteed subset of it — <see cref="Create"/> unions
    /// them in, so no caller can produce a step whose list contradicts its own
    /// words.
    /// </para>
    /// <para>
    /// A set, not a list: the only order a reader can follow is the one the
    /// ingredient list already shows, so ordering is applied where the recipe
    /// is described rather than stored per step and left to disagree with it.
    /// </para>
    /// </remarks>
    public IReadOnlySet<Guid> Uses { get; }

    /// <summary>
    /// How long the step takes, when it is a waiting step. Drives the inline
    /// timer.
    /// </summary>
    public int? DurationSeconds { get; }

    /// <summary>Creates a step.</summary>
    /// <param name="id">Its id, or null for a new one.</param>
    /// <param name="sortOrder">Where it appears.</param>
    /// <param name="segments">Its text.</param>
    /// <param name="uses">What it needs; the mentions are added to it.</param>
    /// <param name="durationSeconds">How long it takes, if it waits.</param>
    public static Result<Step> Create(
        Guid? id,
        int sortOrder,
        IReadOnlyList<StepSegment> segments,
        IReadOnlyCollection<Guid> uses,
        int? durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(uses);

        var length = StepText.Serialise(segments).Length;

        if (length == 0 || length > MaxTextLength)
        {
            return RecipeErrors.InvalidStepText;
        }

        if (durationSeconds is <= 0 or > MaxDurationSeconds)
        {
            return RecipeErrors.InvalidDuration;
        }

        // Counted before the set collapses duplicates, so the cap bounds the
        // work a request can ask for rather than only the result it leaves.
        if (uses.Count > Recipe.MaxIngredients)
        {
            return RecipeErrors.TooManyIngredients;
        }

        // The words win, always: an ingredient the sentence names is needed by
        // the step whatever the caller listed.
        var used = new HashSet<Guid>(uses);

        used.UnionWith(StepText.ReferencedIngredients(segments));

        return new Step(id ?? CulinaId.New(), sortOrder, segments, used, durationSeconds);
    }
}
