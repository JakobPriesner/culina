namespace Contracts.Recipes.GetShared;

/// <summary>
/// A recipe as whoever follows the link sees it.
/// </summary>
/// <remarks>
/// <para>
/// Its own type rather than <see cref="RecipeDetail"/>, and this is the case
/// that type's own remarks describe: the reader here must not have fields the
/// household's own reader does. A stranger holding a link learns the recipe and
/// nothing about who keeps it — no household id, no author, no version to write
/// back against.
/// </para>
/// <para>
/// What it does carry is everything the page draws, ingredient references
/// included, because the point of sharing a Culina recipe rather than a
/// screenshot is that the amounts still scale.
/// </para>
/// </remarks>
public sealed record Response
{
    /// <summary>What it is called.</summary>
    public required string Title { get; init; }

    /// <summary>A short introduction, if there is one.</summary>
    public string? Description { get; init; }

    /// <summary>The language the title and steps are written in.</summary>
    public required string Language { get; init; }

    /// <summary>How many it makes.</summary>
    public required decimal YieldAmount { get; init; }

    /// <summary><c>servings</c> or <c>pieces</c>.</summary>
    public required string YieldKind { get; init; }

    /// <summary>The recipe's own word for what it makes, shown exactly as written.</summary>
    public string? YieldLabel { get; init; }

    /// <summary>Hands-on time.</summary>
    public int? PrepMinutes { get; init; }

    /// <summary>Time in the oven or on the hob.</summary>
    public int? CookMinutes { get; init; }

    /// <summary>Prep plus cook, or null when neither is known.</summary>
    public int? TotalMinutes { get; init; }

    /// <summary>
    /// Whether there is a photograph to fetch.
    /// </summary>
    /// <remarks>
    /// A yes or no and not the image's id: the picture is served under the same
    /// token as the recipe, so its id would be a fact about a household's
    /// storage that answers no question the page asks.
    /// </remarks>
    public required bool HasImage { get; init; }

    /// <summary>Its ingredient groups, in order.</summary>
    public required IReadOnlyList<IngredientGroupContract> Groups { get; init; }

    /// <summary>Its steps, in order.</summary>
    public required IReadOnlyList<StepContract> Steps { get; init; }

    /// <summary>Its tags.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>
    /// The address it was imported from, when it was imported.
    /// </summary>
    /// <remarks>
    /// The address only, not the whole of <see cref="RecipeProvenance"/>: which
    /// connected library it came from and when it was fetched are facts about
    /// the household's setup. Where a recipe was originally published is a
    /// credit, and a page that shows somebody else's recipe should carry it.
    /// </remarks>
    public string? SourceUrl { get; init; }
}
