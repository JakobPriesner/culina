using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Archive;

/// <summary>
/// Everything a household would want to leave with.
/// </summary>
/// <remarks>
/// <para>
/// The right answer to "what if I stop using this", and a self-hosted app owes
/// its users one. It is plain JSON and it is readable: somebody with no Culina
/// at all can open it and find their recipes written out in words.
/// </para>
/// <para>
/// The household's recipes, and the <em>asking person's</em> notes and cooking
/// history. Notes are personal — two people in one kitchen keep separate ones —
/// so an archive carrying everybody's would be one member handing out another's
/// private writing.
/// </para>
/// </remarks>
public static class RecipeArchive
{
    /// <summary>
    /// The archive format's version.
    /// </summary>
    /// <remarks>
    /// Written first and read first. A file with a version this does not know
    /// is refused rather than half-read, because a half-restored recipe is
    /// worse than a failed restore.
    /// </remarks>
    public const int Version = 1;

    /// <summary>How JSON is written and read here.</summary>
    /// <remarks>
    /// Indented, because a person is meant to be able to open it. Nulls are
    /// dropped, because an archive full of `"note": null` is an archive nobody
    /// reads twice.
    /// </remarks>
    public static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>A whole archive.</summary>
public sealed record Archive
{
    /// <summary>The format's version. See <see cref="RecipeArchive.Version"/>.</summary>
    public required int Culina { get; init; }

    /// <summary>When it was written.</summary>
    public required DateTimeOffset ExportedAt { get; init; }

    /// <summary>What the kitchen was called.</summary>
    public string? Household { get; init; }

    /// <summary>Every recipe in it.</summary>
    public required IReadOnlyList<ArchivedRecipe> Recipes { get; init; }
}

/// <summary>One recipe, with everything needed to write it out again.</summary>
public sealed record ArchivedRecipe
{
    /// <summary>What it is called.</summary>
    public required string Title { get; init; }

    /// <summary>Its introduction.</summary>
    public string? Description { get; init; }

    /// <summary><c>en</c> or <c>de</c>.</summary>
    public required string Language { get; init; }

    /// <summary>How much it makes.</summary>
    public required decimal YieldAmount { get; init; }

    /// <summary><c>servings</c> or <c>pieces</c>.</summary>
    public required string YieldKind { get; init; }

    /// <summary>Hands-on minutes.</summary>
    public int? PrepMinutes { get; init; }

    /// <summary>Cooking minutes.</summary>
    public int? CookMinutes { get; init; }

    /// <summary>Its tags, as slugs.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>Its ingredient groups, in order.</summary>
    public required IReadOnlyList<ArchivedGroup> Groups { get; init; }

    /// <summary>Its steps, in order.</summary>
    public required IReadOnlyList<ArchivedStep> Steps { get; init; }

    /// <summary>Its photograph, if it has one.</summary>
    public ArchivedImage? Image { get; init; }

    /// <summary>What the asking person wrote on it.</summary>
    public string? Note { get; init; }

    /// <summary>When the asking person cooked it.</summary>
    public required IReadOnlyList<DateTimeOffset> Cooked { get; init; }
}

/// <summary>A named part of an ingredient list.</summary>
/// <param name="Name">Its heading, or null for the implicit first group.</param>
/// <param name="Ingredients">Its lines, in order.</param>
public sealed record ArchivedGroup(string? Name, IReadOnlyList<ArchivedIngredient> Ingredients);

/// <summary>One ingredient line.</summary>
/// <param name="Quantity">How much, or null.</param>
/// <param name="Unit">In what, or null.</param>
/// <param name="Name">The shoppable noun.</param>
/// <param name="Note">The preparation.</param>
public sealed record ArchivedIngredient(decimal? Quantity, string? Unit, string Name, string? Note);

/// <summary>One step.</summary>
/// <param name="Segments">Its text, split into words and ingredient references.</param>
/// <param name="DurationSeconds">How long it takes, when it waits.</param>
public sealed record ArchivedStep(IReadOnlyList<ArchivedSegment> Segments, int? DurationSeconds);

/// <summary>
/// A piece of a step.
/// </summary>
/// <param name="Text">The words, for a text segment.</param>
/// <param name="Ingredient">
/// Which ingredient, as its position in the recipe's ingredients read in order.
/// </param>
/// <remarks>
/// A <em>position</em>, never an id. Ids are assigned by whichever database the
/// recipe lands in, so an archive that carried them would restore into steps
/// pointing at nothing — and a step whose amounts have come loose is exactly
/// the failure this app is built to prevent.
/// </remarks>
public sealed record ArchivedSegment(string? Text, int? Ingredient);

/// <summary>A recipe's photograph, carried in the file itself.</summary>
/// <param name="ContentType">What it is.</param>
/// <param name="Data">Its bytes, base64.</param>
/// <remarks>
/// Inline rather than by reference, because a reference into a server you have
/// stopped running is a broken image. It is most of the size of an archive, and
/// worth it: a recipe book with no pictures is a worse thing to be left with.
/// </remarks>
public sealed record ArchivedImage(string ContentType, string Data);
