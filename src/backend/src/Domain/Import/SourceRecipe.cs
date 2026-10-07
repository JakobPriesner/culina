namespace Domain.Import;

/// <summary>A recipe as some other app has it, in the one shape this app reads.</summary>
/// <remarks>
/// The seam: every source maps into this, so nothing above <c>Infrastructure</c> sees a foreign type.
/// Structured rather than text lines, and only identity and title are required.
/// </remarks>
public sealed record SourceRecipe
{
    /// <summary>What the other app calls it. Its identity over there, and ours for it.</summary>
    public required string ExternalId { get; init; }

    /// <summary>Its name.</summary>
    public required string Title { get; init; }

    /// <summary>Its introduction, when it has one.</summary>
    public string? Description { get; init; }

    /// <summary>The page to go back and look at, when there is one.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>A picture of it, when there is one.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>What it says it makes.</summary>
    public decimal? Servings { get; init; }

    /// <summary>Hands-on time, in minutes.</summary>
    public int? PrepMinutes { get; init; }

    /// <summary>Time in the oven or on the hob, in minutes.</summary>
    public int? CookMinutes { get; init; }

    /// <summary>Its tags, as the other app wrote them.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Its ingredients, grouped as the other app grouped them.</summary>
    public IReadOnlyList<SourceIngredientGroup> Groups { get; init; } = [];

    /// <summary>Its instructions, one step each.</summary>
    public IReadOnlyList<SourceStep> Steps { get; init; } = [];

    /// <summary>Every ingredient, across all groups.</summary>
    public IEnumerable<SourceIngredient> Ingredients => Groups.SelectMany(group => group.Ingredients);
}

/// <summary>A named run of ingredients, or an unnamed one.</summary>
public sealed record SourceIngredientGroup(string? Name, IReadOnlyList<SourceIngredient> Ingredients);

/// <summary>One ingredient, as the other app has it. The unit is not normalised: the unit vocabulary is open.</summary>
public sealed record SourceIngredient(decimal? Amount, string? Unit, string Name, string? Note);

/// <summary>One instruction, as words and references to the recipe's own ingredients.</summary>
/// <remarks>Segments, not a string, so an amount referenced in a step keeps scaling with the servings.</remarks>
public sealed record SourceStep(IReadOnlyList<SourceStepSegment> Segments, int? Seconds)
{
    /// <summary>A step that is nothing but words.</summary>
    public SourceStep(string text, int? seconds)
        : this([new SourceTextSegment(text)], seconds)
    {
    }
}

/// <summary>One piece of a step's instruction.</summary>
public abstract record SourceStepSegment;

/// <summary>Literal words.</summary>
public sealed record SourceTextSegment(string Value) : SourceStepSegment;

/// <summary>The step pointing at one of the recipe's ingredients, by zero-based position in <see cref="SourceRecipe.Ingredients"/>.</summary>
public sealed record SourceIngredientReference(int Index) : SourceStepSegment;

/// <summary>One page of somebody's library, on the way to being chosen from.</summary>
/// <remarks>
/// Recipes are summaries (groups and steps empty) so browsing does not fetch everything. <c>NextPage</c>
/// is an opaque token because the other app decides how it pages.
/// </remarks>
public sealed record SourcePage(
    IReadOnlyList<SourceRecipe> Recipes,
    string? NextPage,
    int? Total);
