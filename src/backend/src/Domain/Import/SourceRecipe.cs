namespace Domain.Import;

/// <summary>
/// A recipe as some other app has it, in the one shape this app reads.
/// </summary>
/// <remarks>
/// <para>
/// The seam. Every connected source — Tandoor today, Mealie next — maps its own
/// vocabulary into this, and nothing above <c>Infrastructure</c> ever sees a
/// foreign type. Without it, "support another app" would mean a second import
/// handler, a second mapping into a <see cref="Recipes.Recipe"/>, and two sets
/// of rules about what a missing serving size means.
/// </para>
/// <para>
/// Structured rather than lines of text, because the apps worth connecting to
/// are structured: Tandoor knows that 200 and g and Mehl are three things, and
/// flattening them to "200 g Mehl" so that a parser can guess them apart again
/// would be throwing away the only advantage a real API has over a web page.
/// The pasted-text path keeps its heuristics; this one does not need them.
/// </para>
/// <para>
/// Nothing here is required except the identity and the title, because nothing
/// else is reliably there. A recipe with no steps is an ordinary thing to find
/// in a library somebody has been keeping for five years.
/// </para>
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
/// <param name="Name">What the group is called, or null for the plain list.</param>
/// <param name="Ingredients">What is in it, in order.</param>
public sealed record SourceIngredientGroup(string? Name, IReadOnlyList<SourceIngredient> Ingredients);

/// <summary>
/// One ingredient, as the other app has it.
/// </summary>
/// <param name="Amount">How much, or null when it does not say.</param>
/// <param name="Unit">
/// In what, written as the other app writes it — <c>g</c>, <c>EL</c>,
/// <c>Tasse</c>. Not normalised here: this app's own unit vocabulary is open,
/// so a unit somebody invented over there is a unit here too.
/// </param>
/// <param name="Name">What it is.</param>
/// <param name="Note">What to do to it first: "finely chopped".</param>
public sealed record SourceIngredient(decimal? Amount, string? Unit, string Name, string? Note);

/// <summary>One instruction.</summary>
/// <param name="Text">What to do.</param>
/// <param name="Seconds">How long it takes, when the other app said.</param>
public sealed record SourceStep(string Text, int? Seconds);

/// <summary>
/// One page of somebody's library, on the way to being chosen from.
/// </summary>
/// <param name="Recipes">
/// What is on this page. Summaries: a browse of a thousand recipes must not
/// fetch a thousand recipes, so the groups and steps are empty here and filled
/// in only for the ones somebody actually chooses.
/// </param>
/// <param name="NextPage">
/// The page after this one, or null at the end. An opaque token rather than a
/// number, because the app on the other side decides how it pages and this must
/// not assume it counts.
/// </param>
/// <param name="Total">How many there are altogether, when the other app says.</param>
public sealed record SourcePage(
    IReadOnlyList<SourceRecipe> Recipes,
    string? NextPage,
    int? Total);
