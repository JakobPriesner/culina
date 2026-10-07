namespace Contracts.Recipes.GetAll;

/// <summary>A page of recipes.</summary>
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
/// <remarks>Each one leaves between a fifth and four fifths of the results; anything else is a wasted tap.</remarks>
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
/// <remarks>Removing a chip deletes its <c>start</c>–<c>end</c> span from the query and asks again, so only the server parses.</remarks>
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

    /// <summary>
    /// For an ingredient or an exclusion, the thing itself as typed —
    /// "Kartoffeln" when <c>text</c> is the whole "was kann ich mit Kartoffeln
    /// machen?" — for labelling a chip whose span removes more.
    /// </summary>
    public string? Word { get; init; }
}

/// <summary>
/// A recipe as it appears in a list.
/// </summary>
public sealed record RecipeSummary
{
    /// <summary>The recipe's id.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>
    /// The household it belongs to. Another than the one asked about when that
    /// one inherits it — readable and cookable there, changeable only in its
    /// own.
    /// </summary>
    public required Guid HouseholdId { get; init; }

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
    /// <remarks>Null for nearly every recipe; shown exactly as written, never pluralised or translated.</remarks>
    public string? YieldLabel { get; init; }

    /// <summary>Its tags.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>How many times the caller has made it.</summary>
    public required int CookCount { get; init; }

    /// <summary>
    /// When the caller last made it, or null if they never have.
    /// </summary>
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

    /// <summary>
    /// The diet the query asked for, when this recipe keeps it only because
    /// nothing in it says otherwise — <c>vegetarian</c> or <c>vegan</c>; null
    /// when somebody said so, and whenever no diet was asked for.
    /// </summary>
    public string? PresumedDiet { get; init; }
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
public sealed record IngredientMatch
{
    /// <summary>How many of the named ingredients this recipe uses.</summary>
    public required int Matched { get; init; }

    /// <summary>How many were named.</summary>
    public required int Requested { get; init; }

    /// <summary>How many other ingredients it still needs.</summary>
    public required int Missing { get; init; }
}
