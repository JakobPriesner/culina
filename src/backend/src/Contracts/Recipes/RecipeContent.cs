namespace Contracts.Recipes;

/// <summary>An ingredient group and its lines.</summary>
public sealed record IngredientGroupContract
{
    /// <summary>The group's id. Omit to create a new one.</summary>
    public Guid? GroupId { get; init; }

    /// <summary>
    /// The heading, or null for the implicit first group. A recipe with one
    /// unnamed group renders as a plain list.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>Its ingredient lines, in order.</summary>
    public required IReadOnlyList<IngredientContract> Ingredients { get; init; }
}

/// <summary>One line of the ingredient list.</summary>
public sealed record IngredientContract
{
    /// <summary>The line's id. Omit to create a new one; steps refer to it.</summary>
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
    /// <summary>The step's id. Omit to create a new one.</summary>
    public Guid? StepId { get; init; }

    /// <summary>
    /// What this step is called — "Prepare the base".
    /// </summary>
    /// <remarks>
    /// Null for most steps, and a client must then label the step by its
    /// position. It is a name for this one step, not a heading over the ones
    /// that follow it.
    /// </remarks>
    public string? Title { get; init; }

    /// <summary>Its text, split into words and ingredient references.</summary>
    public required IReadOnlyList<StepSegmentContract> Segments { get; init; }

    /// <summary>How long it takes, when it waits. Drives the inline timer.</summary>
    public int? DurationSeconds { get; init; }

    /// <summary>
    /// Everything the step needs, as ingredient ids: what to get out before
    /// starting it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An ingredient the text mentions is always included, listed here or not,
    /// so omitting this field writes exactly what the sentence names — which is
    /// what a client that has never heard of it was already sending.
    /// </para>
    /// <para>
    /// Read back in the recipe's own ingredient order. Order within a step is
    /// not stored: the only order a reader can follow is the one the ingredient
    /// list shows.
    /// </para>
    /// </remarks>
    public IReadOnlyList<Guid>? Uses { get; init; }
}

/// <summary>
/// One piece of a step: either words, or a reference to an ingredient.
/// </summary>
/// <remarks>
/// <para>
/// One record with a <c>type</c> discriminator rather than a polymorphic union.
/// A union would generate as a TypeScript discriminated union that every client
/// has to narrow before touching a field, and it needs serializer configuration
/// on both sides; this shape reads the same and generates cleanly.
/// </para>
/// <para>
/// On a read, an ingredient segment carries the ingredient's name and
/// <b>base</b> amount, so the client can render the step without a lookup and
/// scale it without a round trip. On a write, only <c>type</c> and
/// <c>recipeIngredientId</c> are read.
/// </para>
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
