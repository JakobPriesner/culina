namespace Domain.Suggestions;

/// <summary>Why we are being asked, which selects a preset (pool size, diversity, personal vs. contextual lean).</summary>
/// <remarks>Three, deliberately: screen-specific behaviour belongs in the context's other fields, or modes drift apart.</remarks>
public enum SuggestionPurpose
{
    /// <summary>Rank the whole library. Paged, so no diversity pass and no exploration.</summary>
    Browse = 0,

    /// <summary>A small set to choose dinner from. Bounded, pushed apart, explained.</summary>
    Decide = 1,

    /// <summary>Recipes like one particular recipe; personal taste applies quietly.</summary>
    Like = 2
}
