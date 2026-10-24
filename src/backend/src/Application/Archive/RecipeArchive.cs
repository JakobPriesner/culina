using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Archive;

/// <summary>Everything a household would want to leave with, as plain readable JSON.</summary>
/// <remarks>
/// The household's recipes plus the <em>asking person's</em> notes and cooking history; notes are
/// personal, so an archive must not carry other members' private writing.
/// </remarks>
public static class RecipeArchive
{
    /// <summary>The archive format's version; a file with an unknown version is refused, not half-read.</summary>
    public const int Version = 1;

    /// <summary>How JSON is written and read: indented for people, nulls dropped.</summary>
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

    /// <summary>Its cookbooks. Optional, so archives written before cookbooks travelled still restore.</summary>
    public IReadOnlyList<ArchivedCookbook>? Cookbooks { get; init; }

    /// <summary>What the household said its barcodes are. Optional, so archives written before product memory still restore.</summary>
    public IReadOnlyList<ArchivedProduct>? Products { get; init; }
}

/// <summary>What the household itself said one barcode is; what it only inherits belongs to the household it came from.</summary>
public sealed record ArchivedProduct(
    string Gtin,
    string Name,
    string? Ingredient,
    decimal? Quantity,
    string? Unit,
    int? CookMinutes,
    string? Note);

/// <summary>A cookbook: a chosen shelf by the positions of its recipes, or a smart one by its rules.</summary>
/// <remarks>
/// Positions into <see cref="Archive.Recipes"/>, for the reason <see cref="ArchivedSegment"/> gives. A recipe on a
/// shelf that is not in the archive (one another kitchen lent) has no position and is left off.
/// </remarks>
public sealed record ArchivedCookbook
{
    /// <summary>What it is called.</summary>
    public required string Name { get; init; }

    /// <summary>What it is for.</summary>
    public string? Description { get; init; }

    /// <summary>A chosen shelf's recipes, in shelf order; null on a smart cookbook.</summary>
    public IReadOnlyList<int>? Recipes { get; init; }

    /// <summary>A smart cookbook's rules; null on a chosen shelf.</summary>
    public ArchivedRules? Rules { get; init; }

    /// <summary>The position of the recipe chosen for the cover, or null for the first photographs.</summary>
    public int? Cover { get; init; }
}

/// <summary>What a smart cookbook asks for.</summary>
public sealed record ArchivedRules(IReadOnlyList<string> Tags, IReadOnlyList<string> Ingredients, int? MaxMinutes);

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

    /// <summary>The recipe's own word for what it makes. Optional, so older archives still restore.</summary>
    public string? YieldLabel { get; init; }

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
public sealed record ArchivedGroup(string? Name, IReadOnlyList<ArchivedIngredient> Ingredients);

/// <summary>One ingredient line.</summary>
public sealed record ArchivedIngredient(decimal? Quantity, string? Unit, string Name, string? Note);

/// <summary>One step.</summary>
/// <remarks>
/// <c>Uses</c> (positions in the recipe's ingredients, as in <see cref="ArchivedSegment"/>) and
/// <c>Title</c> default to null so older archives restore without a new format version.
/// </remarks>
public sealed record ArchivedStep(
    IReadOnlyList<ArchivedSegment> Segments,
    int? DurationSeconds,
    IReadOnlyList<int>? Uses = null,
    string? Title = null);

/// <summary>A piece of a step: text, or an ingredient given by position.</summary>
/// <remarks>
/// A <em>position</em>, never an id: ids are assigned by the receiving database, so carried ids would
/// restore as dangling references.
/// </remarks>
public sealed record ArchivedSegment(string? Text, int? Ingredient);

/// <summary>A recipe's photograph, carried in the file itself.</summary>
/// <remarks>Inline rather than by reference, since a reference into a stopped server is a broken image.</remarks>
public sealed record ArchivedImage(string ContentType, string Data);
