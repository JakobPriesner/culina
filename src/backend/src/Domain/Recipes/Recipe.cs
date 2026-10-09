using Domain.Shared;

namespace Domain.Recipes;

/// <summary>A recipe, owned by a household. Only the title is required.</summary>
public sealed class Recipe
{
    /// <summary>More ingredients than anyone could cook from.</summary>
    public const int MaxIngredients = 200;

    /// <summary>More steps than anyone could follow.</summary>
    public const int MaxSteps = 100;

    /// <summary>The longest introduction a recipe may have, in characters.</summary>
    public const int MaxDescriptionLength = 2000;

    /// <summary>More tags than anyone could browse by.</summary>
    public const int MaxTags = 25;

    /// <summary>A tag is a word or two, not a sentence.</summary>
    public const int MaxTagLength = 40;

    /// <summary>A week, in minutes: the longest a recipe may claim to take.</summary>
    public const int MaxMinutes = 10_080;

    private Recipe(
        Guid id,
        Guid householdId,
        RecipeTitle title,
        Guid createdBy,
        Language language,
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
        Language = language;
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

    /// <summary>Derived, never stored: a stored total would eventually disagree with its parts.</summary>
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
    /// <remarks>
    /// The language is required, not defaulted: the search index picks its stemmer from it.
    /// </remarks>
    public static Recipe Create(
        Guid householdId,
        RecipeTitle title,
        Guid createdBy,
        Language language,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(title);

        return new Recipe(CulinaId.New(), householdId, title, createdBy, language, now, now, version: 1);
    }

    /// <summary>Rebuilds a recipe from storage.</summary>
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

        // English until Describe says otherwise; every read path applies the row's language.
        return new Recipe(
            id, householdId, title, createdBy, Language.En, createdAt, updatedAt, version);
    }

    /// <summary>Sets everything that is not the ingredient list or the steps.</summary>
    public Result Describe(RecipeDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        if (OutOfRange(details.PrepMinutes) || OutOfRange(details.CookMinutes))
        {
            return RecipeErrors.InvalidDuration;
        }

        var description = string.IsNullOrWhiteSpace(details.Description) ? null : details.Description.Trim();

        if (description is { Length: > MaxDescriptionLength })
        {
            return RecipeErrors.InvalidDescription;
        }

        if (details.Tags.Count > MaxTags)
        {
            return RecipeErrors.TooManyTags;
        }

        if (details.Tags.Any(tag => string.IsNullOrWhiteSpace(tag) || tag.Trim().Length > MaxTagLength))
        {
            return RecipeErrors.InvalidTag;
        }

        Title = details.Title;
        Description = description;
        Language = details.Language;
        Yield = details.Yield;
        PrepMinutes = details.PrepMinutes;
        CookMinutes = details.CookMinutes;
        Tags = details.Tags;
        UpdatedAt = now;

        return Result.Success();
    }

    /// <summary>Replaces the ingredient list and the steps together.</summary>
    /// <remarks>Together because steps may only reference ingredients the recipe has.</remarks>
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

        // Counted before the set collapses duplicates: the limit is on lines, not distinct ids.
        if (lines > MaxIngredients)
        {
            return RecipeErrors.TooManyIngredients;
        }

        // Duplicate ids would otherwise surface as a primary-key violation (500) on insert.
        if (ingredientIds.Count != lines)
        {
            return RecipeErrors.DuplicateIngredient;
        }

        if (groups.DistinctBy(group => group.Id).Count() != groups.Count)
        {
            return RecipeErrors.DuplicateGroup;
        }

        if (steps.Count > MaxSteps)
        {
            return RecipeErrors.TooManySteps;
        }

        // Steps are updated in place by id, so a duplicate would silently overwrite.
        if (steps.DistinctBy(step => step.Id).Count() != steps.Count)
        {
            return RecipeErrors.DuplicateStep;
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

    /// <summary>The same recipe in another household, with ids of its own.</summary>
    /// <remarks>
    /// Every ingredient gets a new id and steps are remapped to them. The image is shared by reference.
    /// </remarks>
    public Result<Recipe> CopyInto(Guid householdId, Guid createdBy, DateTimeOffset now)
    {
        var copy = Create(householdId, Title, createdBy, Language, now);
        var renamed = new Dictionary<Guid, Guid>();

        var groups = Groups
            .Select(group => group.Ingredients
                .Select(line => RecipeIngredient
                    .Create(null, line.SortOrder, line.Quantity, line.Name, line.Note)
                    .Tap(created => renamed[line.Id] = created.Id))
                .Collect()
                .Bind(lines => IngredientGroup.Create(null, group.Name, group.SortOrder, lines)))
            .Collect();

        var steps = groups.Bind(_ => Steps
            .Select(step => Step.Create(
                null,
                step.SortOrder,
                [.. step.Segments.Select(segment => segment is IngredientSegment mention
                    ? new IngredientSegment(renamed[mention.RecipeIngredientId])
                    : segment)],
                [.. step.Uses.Select(id => renamed[id])],
                step.DurationSeconds,
                step.Title))
            .Collect());

        return groups.Bind(lines => steps
            .Bind(written => copy.Describe(
                    new RecipeDetails(Title, Description, Language, Yield, PrepMinutes, CookMinutes, Tags),
                    now)
                .Bind(() => copy.SetContents(lines, written, now))
                .Map(() => copy)));
    }

    /// <summary>Attaches or clears the hero image.</summary>
    public void SetImage(Guid? imageId, DateTimeOffset now)
    {
        ImageId = imageId;
        UpdatedAt = now;
    }

    /// <summary>Records that this recipe has been written to storage.</summary>
    public void AcceptVersion(long version) => Version = version;

    // Prefers "step N still needs that ingredient" for a removed ingredient over the unknown-reference
    // error, checked across all steps first so the answer does not depend on set order. Also keeps the
    // reference table's foreign key from failing as a database error.
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
public sealed record RecipeDetails(
    RecipeTitle Title,
    string? Description,
    Language Language,
    Yield Yield,
    int? PrepMinutes,
    int? CookMinutes,
    IReadOnlyList<string> Tags);
