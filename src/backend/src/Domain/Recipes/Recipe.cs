using Domain.Shared;

namespace Domain.Recipes;

/// <summary>
/// A recipe, owned by a household.
/// </summary>
/// <remarks>
/// Only the title is required. Everything else can be filled in later, because
/// the most common reason to open the create form is to write something down
/// before forgetting it.
/// </remarks>
public sealed class Recipe
{
    /// <summary>More ingredients than anyone could cook from.</summary>
    public const int MaxIngredients = 200;

    /// <summary>More steps than anyone could follow.</summary>
    public const int MaxSteps = 100;

    /// <summary>A week, in minutes: the longest a recipe may claim to take.</summary>
    public const int MaxMinutes = 10_080;

    private Recipe(
        Guid id,
        Guid householdId,
        RecipeTitle title,
        Guid createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        Id = id;
        HouseholdId = householdId;
        Title = title;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
        Groups = [IngredientGroup.Implicit()];
        Steps = [];
        Tags = [];
        Language = Language.En;
        Yield = Yield.Default;
    }

    /// <summary>The recipe's id.</summary>
    public Guid Id { get; }

    /// <summary>Which household owns it.</summary>
    public Guid HouseholdId { get; }

    /// <summary>What it is called.</summary>
    public RecipeTitle Title { get; private set; }

    /// <summary>A short introduction, if there is one.</summary>
    public string? Description { get; private set; }

    /// <summary>The language the title and steps are written in.</summary>
    public Language Language { get; private set; }

    /// <summary>What it makes.</summary>
    public Yield Yield { get; private set; }

    /// <summary>Hands-on time.</summary>
    public int? PrepMinutes { get; private set; }

    /// <summary>Time in the oven or on the hob.</summary>
    public int? CookMinutes { get; private set; }

    /// <summary>
    /// Total time, derived and never stored: a stored total is a second source
    /// of truth that eventually disagrees with its parts.
    /// </summary>
    public int? TotalMinutes => PrepMinutes is null && CookMinutes is null
        ? null
        : (PrepMinutes ?? 0) + (CookMinutes ?? 0);

    /// <summary>Its hero image, if it has one.</summary>
    public Guid? ImageId { get; private set; }

    /// <summary>Its ingredient groups, in order.</summary>
    public IReadOnlyList<IngredientGroup> Groups { get; private set; }

    /// <summary>Its steps, in order.</summary>
    public IReadOnlyList<Step> Steps { get; private set; }

    /// <summary>Its tag slugs.</summary>
    public IReadOnlyList<string> Tags { get; private set; }

    /// <summary>Who wrote it down.</summary>
    public Guid CreatedBy { get; }

    /// <summary>When it was written down.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When it last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Incremented by every write; the ETag is derived from it.</summary>
    public long Version { get; private set; }

    /// <summary>Every ingredient line, across all groups.</summary>
    public IEnumerable<RecipeIngredient> Ingredients =>
        Groups.SelectMany(group => group.Ingredients);

    /// <summary>Starts a new recipe.</summary>
    /// <param name="householdId">Which household owns it.</param>
    /// <param name="title">What it is called.</param>
    /// <param name="createdBy">Who wrote it down.</param>
    /// <param name="now">The injected current time.</param>
    public static Recipe Create(
        Guid householdId,
        RecipeTitle title,
        Guid createdBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(title);

        return new Recipe(CulinaId.New(), householdId, title, createdBy, now, now, version: 1);
    }

    /// <summary>Rebuilds a recipe from storage.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="householdId">Which household owns it.</param>
    /// <param name="title">What it is called.</param>
    /// <param name="createdBy">Who wrote it down.</param>
    /// <param name="createdAt">When it was written down.</param>
    /// <param name="updatedAt">When it last changed.</param>
    /// <param name="version">The stored version.</param>
    public static Recipe Restore(
        Guid id,
        Guid householdId,
        RecipeTitle title,
        Guid createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        ArgumentNullException.ThrowIfNull(title);

        return new Recipe(id, householdId, title, createdBy, createdAt, updatedAt, version);
    }

    /// <summary>Sets everything that is not the ingredient list or the steps.</summary>
    /// <param name="details">The new values.</param>
    /// <param name="now">The injected current time.</param>
    public Result Describe(RecipeDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        if (OutOfRange(details.PrepMinutes) || OutOfRange(details.CookMinutes))
        {
            return RecipeErrors.InvalidDuration;
        }

        Title = details.Title;
        Description = string.IsNullOrWhiteSpace(details.Description) ? null : details.Description.Trim();
        Language = details.Language;
        Yield = details.Yield;
        PrepMinutes = details.PrepMinutes;
        CookMinutes = details.CookMinutes;
        Tags = details.Tags;
        UpdatedAt = now;

        return Result.Success();
    }

    /// <summary>
    /// Replaces the ingredient list and the steps together.
    /// </summary>
    /// <param name="groups">The new ingredient groups.</param>
    /// <param name="steps">The new steps.</param>
    /// <param name="now">The injected current time.</param>
    /// <remarks>
    /// They are set in one call because they are not independent: a step may
    /// only refer to an ingredient the recipe has, so validating either alone
    /// would let a save pass that the pair does not.
    /// </remarks>
    public Result SetContents(
        IReadOnlyList<IngredientGroup> groups,
        IReadOnlyList<Step> steps,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(steps);

        var lines = groups.Sum(group => group.Ingredients.Count);

        var ingredientIds = groups.SelectMany(group => group.Ingredients)
            .Select(ingredient => ingredient.Id)
            .ToHashSet();

        // Counted before the set collapses them, for the same reason the step
        // cap is: the limit is on lines a cook reads, not on distinct ids.
        if (lines > MaxIngredients)
        {
            return RecipeErrors.TooManyIngredients;
        }

        // Two lines claiming one id would pass every check here and then fail
        // as a primary-key violation on insert, which reaches the caller as a
        // 500 rather than as the validation failure it is.
        if (ingredientIds.Count != lines)
        {
            return RecipeErrors.DuplicateIngredient;
        }

        if (steps.Count > MaxSteps)
        {
            return RecipeErrors.TooManySteps;
        }

        if (DanglingReference(steps, ingredientIds) is { } failure)
        {
            return failure;
        }

        Groups = groups.Count == 0 ? [IngredientGroup.Implicit()] : groups;
        Steps = steps;
        UpdatedAt = now;

        return Result.Success();
    }

    /// <summary>Attaches or clears the hero image.</summary>
    /// <param name="imageId">The stored image, or null to remove it.</param>
    /// <param name="now">The injected current time.</param>
    public void SetImage(Guid? imageId, DateTimeOffset now)
    {
        ImageId = imageId;
        UpdatedAt = now;
    }

    /// <summary>Records that this recipe has been written to storage.</summary>
    /// <param name="version">The version the database assigned.</param>
    public void AcceptVersion(long version) => Version = version;

    /// <summary>
    /// Reports a step that needs an ingredient the recipe no longer has.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An ingredient that existed before the change gets the more helpful
    /// "step N still needs that ingredient"; one that never existed gets the
    /// plain unknown-reference error. The distinction matters because the
    /// first is an editing mistake with an obvious fix and the second is a
    /// malformed request.
    /// </para>
    /// <para>
    /// The removals are looked for across every step before the unknown ids
    /// are, because a step's needs are a set with no order of its own: deciding
    /// between the two messages by whichever id came out of the set first would
    /// make the same request answer differently on different runs.
    /// </para>
    /// <para>
    /// This is also what keeps the stored reference index honest. Every id here
    /// is written to a table with a foreign key onto the ingredient rows, so a
    /// step allowed through with an id the recipe does not have would fail as a
    /// database error rather than as a named failure.
    /// </para>
    /// </remarks>
    private Error? DanglingReference(IReadOnlyList<Step> steps, HashSet<Guid> ingredientIds)
    {
        var removed = Ingredients.Select(ingredient => ingredient.Id).ToHashSet();

        removed.ExceptWith(ingredientIds);

        foreach (var step in steps)
        {
            if (step.Uses.Any(removed.Contains))
            {
                return RecipeErrors.IngredientInUse(step.SortOrder + 1);
            }
        }

        return steps.SelectMany(step => step.Uses).Any(used => !ingredientIds.Contains(used))
            ? RecipeErrors.UnknownIngredientReference
            : null;
    }

    private static bool OutOfRange(int? minutes) => minutes is < 0 or > MaxMinutes;
}

/// <summary>Everything about a recipe except its ingredients and steps.</summary>
/// <param name="Title">What it is called.</param>
/// <param name="Description">A short introduction.</param>
/// <param name="Language">The language it is written in.</param>
/// <param name="Yield">What it makes.</param>
/// <param name="PrepMinutes">Hands-on time.</param>
/// <param name="CookMinutes">Time in the oven or on the hob.</param>
/// <param name="Tags">Its tag slugs.</param>
public sealed record RecipeDetails(
    RecipeTitle Title,
    string? Description,
    Language Language,
    Yield Yield,
    int? PrepMinutes,
    int? CookMinutes,
    IReadOnlyList<string> Tags);
