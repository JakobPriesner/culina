namespace Contracts.Recipes.RecordCooked;

/// <summary>
/// That you cooked this.
/// </summary>
/// <remarks>
/// Every field is optional, and an empty body is the expected case: one tap is
/// the whole interaction, and anything more would be a form standing between
/// someone and the thing they just finished cooking.
/// </remarks>
public sealed record Request
{
    /// <summary>When, if not now.</summary>
    public DateTimeOffset? MadeAt { get; init; }

    /// <summary>How much you made.</summary>
    public decimal? Servings { get; init; }

    /// <summary>Anything worth remembering.</summary>
    public string? Note { get; init; }
}
