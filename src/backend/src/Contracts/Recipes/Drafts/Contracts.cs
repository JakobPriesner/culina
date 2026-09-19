namespace Contracts.Recipes.Drafts;

/// <summary>Asks the assistant for a recipe.</summary>
/// <remarks>
/// One request with a <c>kind</c> rather than three endpoints, because the three
/// are the same call underneath — words in, one recipe out — and three routes
/// would be three places to remember the budget check.
/// </remarks>
public sealed record Request
{
    /// <summary>
    /// What is being asked for: <c>idea</c>, <c>text</c> or <c>revision</c>.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>Whose kitchen it is for.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>
    /// The material, for <c>idea</c> and <c>text</c>.
    /// </summary>
    /// <remarks>
    /// A sentence about dinner, or a whole recipe pasted out of a message. The
    /// same field for both because it is the same thing from here: words the
    /// person supplied, which the server never treats as an instruction.
    /// </remarks>
    public string? Material { get; init; }

    /// <summary>Which recipe to rewrite, for <c>revision</c>.</summary>
    public Guid? RecipeId { get; init; }

    /// <summary>
    /// The language to answer in: <c>en</c> or <c>de</c>.
    /// </summary>
    /// <remarks>
    /// Sent rather than inferred from the material, because inferring it gets
    /// the common case wrong: a German household pasting an English page wants
    /// a German recipe, and a model reading the page would answer in English.
    /// Ignored for a revision, where the recipe's own language wins.
    /// </remarks>
    public string? Language { get; init; }
}

/// <summary>
/// A recipe the assistant wrote. Not saved, and not a recipe yet.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is created by asking. This comes back to be read beside whatever was
/// there before and accepted a field at a time, which is the whole reason the
/// capability is safe to offer: an assistant that silently rewrote somebody's
/// recipe would be one nobody could trust with the one they cook from.
/// </para>
/// <para>
/// Structured rather than lines of text, unlike the web import next door. That
/// one returns what a page said and lets the client's parser have a go, because
/// a page gives you a sentence; a model was asked for fields and answered in
/// them, and pushing that back through a heuristic would lose what it knew.
/// </para>
/// </remarks>
public sealed record Response
{
    /// <summary>What the assistant called it.</summary>
    public string? Title { get; init; }

    /// <summary>A sentence or two about it.</summary>
    public string? Description { get; init; }

    /// <summary>How many it makes.</summary>
    public decimal? YieldAmount { get; init; }

    /// <summary>What it makes: servings, or a cake.</summary>
    public string? YieldLabel { get; init; }

    /// <summary>Minutes of hands-on work.</summary>
    public int? PrepMinutes { get; init; }

    /// <summary>Minutes of cooking.</summary>
    public int? CookMinutes { get; init; }

    /// <summary>The ingredient groups, in order.</summary>
    public required IReadOnlyList<DraftGroupContract> Groups { get; init; }

    /// <summary>The steps, in order.</summary>
    public required IReadOnlyList<DraftStepContract> Steps { get; init; }

    /// <summary>What to file it under.</summary>
    public required IReadOnlyList<string> Tags { get; init; }
}

/// <summary>A heading and the lines under it.</summary>
public sealed record DraftGroupContract
{
    /// <summary>The heading, or null for the implicit first group.</summary>
    public string? Name { get; init; }

    /// <summary>Its lines, in order.</summary>
    public required IReadOnlyList<DraftIngredientContract> Ingredients { get; init; }
}

/// <summary>One ingredient line.</summary>
public sealed record DraftIngredientContract
{
    /// <summary>How much, or null when the recipe does not say.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>
    /// In what, or null.
    /// </summary>
    /// <remarks>
    /// Already checked against what the app can store, so a client may use it
    /// as it stands. A unit the assistant invented that could never be a unit —
    /// one with a digit in it — is dropped here rather than being handed on to
    /// fail later.
    /// </remarks>
    public string? Unit { get; init; }

    /// <summary>The shoppable noun.</summary>
    public required string Name { get; init; }

    /// <summary>The preparation.</summary>
    public string? Note { get; init; }
}

/// <summary>One instruction.</summary>
/// <remarks>
/// Plain text, not segments. Which words in a step name an ingredient is a
/// question the client already answers when somebody types a step, and asking a
/// model to mark them up too would be two sources of truth for one fact.
/// </remarks>
public sealed record DraftStepContract
{
    /// <summary>What this step is called, when it is called anything.</summary>
    public string? Title { get; init; }

    /// <summary>What to do.</summary>
    public required string Text { get; init; }

    /// <summary>How long it waits, when it waits.</summary>
    public int? DurationSeconds { get; init; }
}
