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

    private Step(Guid id, int sortOrder, IReadOnlyList<StepSegment> segments, int? durationSeconds)
    {
        Id = id;
        SortOrder = sortOrder;
        Segments = segments;
        DurationSeconds = durationSeconds;
    }

    /// <summary>The step's id.</summary>
    public Guid Id { get; }

    /// <summary>Where it appears.</summary>
    public int SortOrder { get; }

    /// <summary>Its text, split into words and ingredient references.</summary>
    public IReadOnlyList<StepSegment> Segments { get; }

    /// <summary>
    /// How long the step takes, when it is a waiting step. Drives the inline
    /// timer.
    /// </summary>
    public int? DurationSeconds { get; }

    /// <summary>Creates a step.</summary>
    /// <param name="id">Its id, or null for a new one.</param>
    /// <param name="sortOrder">Where it appears.</param>
    /// <param name="segments">Its text.</param>
    /// <param name="durationSeconds">How long it takes, if it waits.</param>
    public static Result<Step> Create(
        Guid? id,
        int sortOrder,
        IReadOnlyList<StepSegment> segments,
        int? durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var length = StepText.Serialise(segments).Length;

        if (length == 0 || length > MaxTextLength)
        {
            return RecipeErrors.InvalidStepText;
        }

        if (durationSeconds is <= 0 or > MaxDurationSeconds)
        {
            return RecipeErrors.InvalidDuration;
        }

        return new Step(id ?? CulinaId.New(), sortOrder, segments, durationSeconds);
    }

    /// <summary>The ingredients this step mentions.</summary>
    public IReadOnlySet<Guid> ReferencedIngredients => StepText.ReferencedIngredients(Segments);
}
