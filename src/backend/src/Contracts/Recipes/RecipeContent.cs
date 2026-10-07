namespace Contracts.Recipes;

/// <summary>An ingredient group and its lines.</summary>
public sealed record IngredientGroupContract
{
    /// <summary>
    /// The group's id; omit it, or send one the recipe does not have, to create a group with a
    /// server-chosen id.
    /// </summary>
    public Guid? GroupId { get; init; }

    /// <summary>
    /// The heading, or null for the implicit first group; a recipe with one unnamed group renders
    /// as a plain list.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>Its ingredient lines, in order.</summary>
    public required IReadOnlyList<IngredientContract> Ingredients { get; init; }
}

/// <summary>One line of the ingredient list.</summary>
public sealed record IngredientContract
{
    /// <summary>
    /// The line's id, which steps refer to. Omit it, or send an unknown one, to create a line with
    /// a server-chosen id: read ids back from the response, since steps are re-pointed at the new
    /// one.
    /// </summary>
    public Guid? IngredientId { get; init; }

    /// <summary>How much, or null when the recipe does not say.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>In what, or null for a bare count.</summary>
    public string? Unit { get; init; }

    /// <summary>The shoppable noun: "butter".</summary>
    public required string Name { get; init; }

    /// <summary>The preparation: "finely chopped".</summary>
    public string? Note { get; init; }
}

/// <summary>One instruction.</summary>
public sealed record StepContract
{
    /// <summary>
    /// The step's id; omit it, or send one the recipe does not have, to create a step with a
    /// server-chosen id.
    /// </summary>
    public Guid? StepId { get; init; }

    /// <summary>
    /// What this step is called, e.g. "Prepare the base"; null for most steps, which a client
    /// labels by position. A name for this step, not a heading over the following ones.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>Its text, split into words and ingredient references.</summary>
    public required IReadOnlyList<StepSegmentContract> Segments { get; init; }

    /// <summary>How long it takes, when it waits. Drives the inline timer.</summary>
    public int? DurationSeconds { get; init; }

    /// <summary>
    /// Everything the step needs, as ingredient ids: what to get out before starting it.
    /// </summary>
    /// <remarks>
    /// An ingredient the text mentions is always included, so omitting this writes exactly what the
    /// sentence names. Read back in the recipe's ingredient order.
    /// </remarks>
    public IReadOnlyList<Guid>? Uses { get; init; }
}

/// <summary>One piece of a step: either words, or a reference to an ingredient.</summary>
/// <remarks>
/// One record with a <c>type</c> discriminator, not a polymorphic union, which would generate a
/// TypeScript union every client must narrow. On a read an ingredient segment carries name and base
/// amount, so clients render and scale without a lookup; on a write only <c>type</c> and
/// <c>recipeIngredientId</c> are read.
/// </remarks>
public sealed record StepSegmentContract
{
    /// <summary><c>text</c> or <c>ingredient</c>.</summary>
    public required string Type { get; init; }

    /// <summary>The words, for a text segment.</summary>
    public string? Value { get; init; }

    /// <summary>Which ingredient, for an ingredient segment.</summary>
    public Guid? RecipeIngredientId { get; init; }

    /// <summary>The ingredient's name, on a read.</summary>
    public string? Name { get; init; }

    /// <summary>The ingredient's base amount, on a read.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>The ingredient's unit, on a read.</summary>
    public string? Unit { get; init; }
}
