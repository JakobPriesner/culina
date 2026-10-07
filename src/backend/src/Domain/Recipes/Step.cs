using Domain.Shared;

namespace Domain.Recipes;

/// <summary>
/// One instruction, held as segments so an ingredient mention renders its scaled amount (see
/// <see cref="StepText"/>).
/// </summary>
public sealed class Step
{
    /// <summary>The longest step text the database column accepts.</summary>
    public const int MaxTextLength = 4000;

    /// <summary>The longest timer a step may start: one day.</summary>
    public const int MaxDurationSeconds = 86_400;

    /// <summary>
    /// The longest a step's title may be: a tenth of the text, so it cannot become a second copy of
    /// the instruction.
    /// </summary>
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
    /// What this step is called, when it is called anything; null is the ordinary case.
    /// </summary>
    public string? Title { get; }

    /// <summary>Its text, split into words and ingredient references.</summary>
    public IReadOnlyList<StepSegment> Segments { get; }

    /// <summary>Everything the step needs, whether or not the sentence names it.</summary>
    /// <remarks>
    /// A superset of the mentions (<see cref="Create"/> unions them in), so the list never
    /// contradicts the words. A set: ordering is applied from the ingredient list.
    /// </remarks>
    public IReadOnlySet<Guid> Uses { get; }

    /// <summary>
    /// How long the step takes, when it is a waiting step; drives the inline timer.
    /// </summary>
    public int? DurationSeconds { get; }

    /// <summary>Creates a step.</summary>
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

        // Blank is the absence of a name, as for any emptied field in a recipe.
        var named = string.IsNullOrWhiteSpace(title) ? null : title.Trim();

        if (named?.Length > MaxTitleLength)
        {
            return RecipeErrors.InvalidStepTitle;
        }

        if (durationSeconds is <= 0 or > MaxDurationSeconds)
        {
            return RecipeErrors.InvalidDuration;
        }

        // Counted before the set collapses duplicates, so the cap bounds the work asked for.
        if (uses.Count > Recipe.MaxIngredients)
        {
            return RecipeErrors.TooManyIngredients;
        }

        // The words win: an ingredient the sentence names is needed whatever the caller listed.
        var used = new HashSet<Guid>(uses);

        used.UnionWith(StepText.ReferencedIngredients(segments));

        return new Step(id ?? CulinaId.New(), sortOrder, named, segments, used, durationSeconds);
    }
}
