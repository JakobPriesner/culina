namespace Contracts.Recipes;

/// <summary>Where an imported recipe came from. Quiet on purpose: an imported recipe is an ordinary recipe.</summary>
public sealed record RecipeProvenance
{
    /// <summary>Which sort of place: <c>tandoor</c> or <c>web</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The connection it came through, when it still exists.</summary>
    public Guid? SourceId { get; init; }

    /// <summary>What that place called it.</summary>
    public required string ExternalId { get; init; }

    /// <summary>The original, to go and look at.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>When it arrived.</summary>
    public required DateTimeOffset ImportedAt { get; init; }
}
