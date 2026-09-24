namespace Contracts.Recipes.GetAll;

/// <summary>A page of recipes.</summary>
/// <remarks>
/// Always a wrapped object, never a bare array: an array has nowhere to grow
/// paging metadata, and adding it later would be a breaking change.
/// </remarks>
public sealed record Response
{
    /// <summary>The recipes on this page, in the requested order.</summary>
    public required IReadOnlyList<RecipeSummary> Items { get; init; }

    /// <summary>Pass this back as <c>cursor</c> for the next page, or null at the end.</summary>
    public string? NextCursor { get; init; }

    /// <summary>How many recipes match, across all pages.</summary>
    public required int Total { get; init; }

    /// <summary>
    /// What the query was understood to mean, or null when there was no query.
    /// </summary>
    public Interpretation? Interpretation { get; init; }

    /// <summary>
    /// What these results could be narrowed by, counted over all of them — or
    /// null when there was no query, or nothing would split them.
    /// </summary>
    public Facets? Facets { get; init; }
}

/// <summary>
/// Refinements worth offering, computed from the results rather than curated.
/// </summary>
/// <remarks>
/// Each one leaves between a fifth and four fifths of the results: a
/// refinement that removes nothing, or everything, is a tap wasted.
/// </remarks>
public sealed record Facets
{
    /// <summary>Tags, by slug, with the household's own name for each.</summary>
    public required IReadOnlyList<Facet> Tags { get; init; }

    /// <summary>Time ceilings, in minutes.</summary>
    public required IReadOnlyList<Facet> Times { get; init; }

    /// <summary>Cuisines, by the same keys a cuisine reading uses.</summary>
    public required IReadOnlyList<Facet> Cuisines { get; init; }
}

/// <summary>One refinement, and how many results it would leave.</summary>
public sealed record Facet
{
    /// <summary>A tag slug, a number of minutes, or a cuisine key.</summary>
    public required string Value { get; init; }

    /// <summary>The household's name for a tag; null for the others, which a client words itself.</summary>
    public string? Label { get; init; }

    /// <summary>How many of the results it would leave.</summary>
    public required int Count { get; init; }
}

/// <summary>
/// A query, as the server read it.
/// </summary>
/// <remarks>
/// Every applied entry is something the client can draw as a removable chip.
/// Removing one is deleting its <c>start</c>–<c>end</c> span from the query and
/// asking again, so there is one parser, here, and the client never has to be
/// a second one.
/// </remarks>
public sealed record Interpretation
{
    /// <summary>The words that were searched for, once everything below was taken out.</summary>
    public required string FreeText { get; init; }

    /// <summary>What was inferred, in the order it was typed.</summary>
    public required IReadOnlyList<AppliedInference> Applied { get; init; }

    /// <summary>
    /// The words as typed, when nothing matched them and a correction did —
    /// <c>freeText</c> is then the correction. Resend with <c>asTyped=true</c>
    /// to search what was typed instead.
    /// </summary>
    public string? CorrectedFrom { get; init; }

    /// <summary>
    /// Readings set aside because nothing matched all of them, weakest first:
    /// cuisine, meal, ingredient, time. A diet or an exclusion never is.
    /// </summary>
    public IReadOnlyList<AppliedInference>? Relaxed { get; init; }

    /// <summary>
    /// Two readings that cannot both hold — a diet and an ingredient it rules
    /// out — when that is why nothing matched.
    /// </summary>
    public IReadOnlyList<AppliedInference>? Conflict { get; init; }
}

/// <summary>One thing the query was understood to ask.</summary>
public sealed record AppliedInference
{
    /// <summary>
    /// <c>time</c>, <c>quick</c>, <c>diet</c>, <c>meal</c>, <c>cuisine</c>,
    /// <c>ingredient</c> or <c>exclusion</c>.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// What it means: minutes for a time; a stable key for a diet, meal,
    /// cuisine and every ingredient or exclusion the server recognised
    /// (<c>vegetarian</c>, <c>dinner</c>, <c>italian</c>, <c>potato</c>); and
    /// the word as typed for anything it did not.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>The characters it was read from, exactly as typed.</summary>
    public required string Text { get; init; }

    /// <summary>Where those characters begin in the query, in UTF-16 code units.</summary>
    public required int Start { get; init; }

    /// <summary>Where they end, exclusive.</summary>
    public required int End { get; init; }
}

/// <summary>
/// A recipe as it appears in a list.
/// </summary>
/// <remarks>
/// Deliberately not the full recipe: a grid of thirty cards has no use for
/// thirty ingredient lists, and sending them would make the first screen the
/// slowest.
/// </remarks>
public sealed record RecipeSummary
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

    /// <summary>
    /// When the caller last made it, or null if they never have.
    /// </summary>
    /// <remarks>
    /// Read in the same scan as the count, which the cook log's index already
    /// serves, so it costs nothing. It is what lets a card say "last in March"
    /// rather than only "7 times" — and what a suggestion's rediscovery reason
    /// is rendered from, instead of the server sending prose.
    /// </remarks>
    public DateTimeOffset? LastCookedAt { get; init; }

    /// <summary>When it last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// How well it fits the ingredients the caller asked about, when they asked
    /// about any.
    /// </summary>
    public IngredientMatch? IngredientMatch { get; init; }

    /// <summary>
    /// Why it answers the query, when that is not its title — null for a title
    /// match, and for a list without words.
    /// </summary>
    public MatchReason? MatchReason { get; init; }
}

/// <summary>Why a recipe is in a search it does not name in its title.</summary>
public sealed record MatchReason
{
    /// <summary>
    /// <c>ingredient</c>, <c>tag</c>, <c>text</c> (its description or a step)
    /// or <c>concept</c> (only through what it is — "Waffeln" for "Nachtisch").
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// The ingredient or tag as the recipe writes it, or the concept in the
    /// recipe's language; null for text.
    /// </summary>
    public string? Term { get; init; }
}

/// <summary>
/// How well a recipe fits what you have.
/// </summary>
/// <remarks>
/// Rendered as "uses 3 of 3 · 2 more needed". This is the whole of Culina's
/// answer to "what can I cook?": no pantry to maintain, so nothing to go stale.
/// </remarks>
public sealed record IngredientMatch
{
    /// <summary>How many of the named ingredients this recipe uses.</summary>
    public required int Matched { get; init; }

    /// <summary>How many were named.</summary>
    public required int Requested { get; init; }

    /// <summary>How many other ingredients it still needs.</summary>
    public required int Missing { get; init; }
}
