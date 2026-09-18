namespace Contracts.Suggestions.GetAll;

/// <summary>A handful of recipes for one occasion.</summary>
/// <remarks>
/// Wrapped like every other collection, but deliberately without a cursor: this
/// is bounded on purpose. The paging envelope exists for collections that page,
/// and a thing with no next page should not claim one. Ranking the whole
/// library is <c>GET /recipes?sort=suggested</c>, which does page.
/// </remarks>
public sealed record Response
{
    /// <summary>The suggestions, best first.</summary>
    public required IReadOnlyList<Suggestion> Items { get; init; }
}

/// <summary>One recipe, and why it is here.</summary>
public sealed record Suggestion
{
    /// <summary>The recipe's id.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>What it is called.</summary>
    public required string Title { get; init; }

    /// <summary>Its hero image, if it has one.</summary>
    public Guid? ImageId { get; init; }

    /// <summary>Prep plus cook, or null when neither is known.</summary>
    public int? TotalMinutes { get; init; }

    /// <summary>How many it makes.</summary>
    public required decimal YieldAmount { get; init; }

    /// <summary><c>servings</c> or <c>pieces</c>.</summary>
    public required string YieldKind { get; init; }

    /// <summary>
    /// The recipe's own word for what it makes — "Cake", "Gläser", "Blech".
    /// </summary>
    /// <remarks>
    /// Null for nearly every recipe, and a client must then word the yield from
    /// <c>yieldKind</c> in the reader's language. When it is set it replaces
    /// that word and is shown exactly as written — it is one person's noun in
    /// one person's language, so nothing here pluralises or translates it.
    /// </remarks>
    public string? YieldLabel { get; init; }

    /// <summary>Its tags.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>How many times the caller has made it.</summary>
    public required int CookCount { get; init; }

    /// <summary>When the caller last made it, or null.</summary>
    public DateTimeOffset? LastCookedAt { get; init; }

    /// <summary>
    /// When the recipe last changed.
    /// </summary>
    /// <remarks>
    /// Carried so a suggestion is a complete recipe card and the client needs no
    /// second shape for one. A card that had to invent a missing field would be
    /// inventing it on every screen that renders a suggestion.
    /// </remarks>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Why this one, or null when no single term decided it.
    /// </summary>
    /// <remarks>
    /// Null is an ordinary answer and the client must render it as nothing. A
    /// good suggestion with no explanation is fine; an invented explanation is
    /// a lie, and catching one discredits every reason that was true.
    /// </remarks>
    public SuggestionReasonView? Reason { get; init; }
}

/// <summary>Why a recipe was suggested.</summary>
/// <remarks>
/// A code and at most a subject, never a sentence. The wording is the client's,
/// because it is the client that knows which of two languages the person reads
/// — and because prose on the wire cannot be translated after it arrives.
/// </remarks>
public sealed record SuggestionReasonView
{
    /// <summary>
    /// One of <c>affinity</c>, <c>rediscovery</c>, <c>tag</c>,
    /// <c>ingredient</c>, <c>season</c>, <c>slot</c>, <c>household</c>,
    /// <c>fresh</c>, <c>similar</c>. Branch on it.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// What the reason is about — a tag, an ingredient name, or a member's
    /// display name — when it is about something nameable.
    /// </summary>
    public string? Subject { get; init; }
}
