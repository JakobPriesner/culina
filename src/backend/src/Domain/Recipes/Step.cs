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

    /// <summary>The longest a step's title may be.</summary>
    /// <remarks>
    /// A tenth of the text it heads. A title long enough to hold the
    /// instruction is a second copy of the instruction, and the two would
    /// disagree by the third edit.
    /// </remarks>
    public const int MaxTitleLength = 120;

    private Step(
        Guid id,
        int sortOrder,
        string? title,
        IReadOnlyList<StepSegment> segments,
        IReadOnlySet<Guid> uses,
        int? durationSeconds)
    {
        Id = id;
        SortOrder = sortOrder;
        Title = title;
        Segments = segments;
        Uses = uses;
        DurationSeconds = durationSeconds;
    }

    /// <summary>The step's id.</summary>
    public Guid Id { get; }

    /// <summary>Where it appears.</summary>
    public int SortOrder { get; }

    /// <summary>
    /// What this step is called, when it is called anything.
    /// </summary>
    /// <remarks>
    /// Null for most steps, and that is the ordinary case: a step in a short
    /// recipe is "step 3" and naming it would be ceremony. A recipe with a
    /// base, a filling and a glaze is the other case, and there the number is
    /// the least useful thing that could be written above the sentence.
    /// </remarks>
    public string? Title { get; }

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
    /// <param name="title">What it is called, or null to be called by number.</param>
    public static Result<Step> Create(
        Guid? id,
        int sortOrder,
        IReadOnlyList<StepSegment> segments,
        IReadOnlyCollection<Guid> uses,
        int? durationSeconds,
        string? title = null)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(uses);

        var length = StepText.Serialise(segments).Length;

        if (length == 0 || length > MaxTextLength)
        {
            return RecipeErrors.InvalidStepText;
        }

        // Blank is not a name, so it is the absence of one — the same reading
        // an emptied field gets everywhere else in a recipe.
        var named = string.IsNullOrWhiteSpace(title) ? null : title.Trim();

        if (named?.Length > MaxTitleLength)
        {
            return RecipeErrors.InvalidStepTitle;
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

        return new Step(id ?? CulinaId.New(), sortOrder, named, segments, used, durationSeconds);
    }
}
