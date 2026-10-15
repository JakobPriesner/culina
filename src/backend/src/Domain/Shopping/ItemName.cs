using System.Text;
using Domain.Ingredients;
using Domain.Shared;

namespace Domain.Shopping;

/// <summary>
/// What an item is called, plus the folded form two names are compared in, so the list shows their
/// words but merges on ours ("Müsli" and "Muesli").
/// </summary>
public sealed record ItemName
{
    /// <summary>The longest name a shopping list needs.</summary>
    public const int MaxLength = 120;

    private IngredientReading? reading;

    private ItemName(string value, string comparisonKey)
    {
        Value = value;
        ComparisonKey = comparisonKey;
    }

    /// <summary>What the person wrote.</summary>
    public string Value { get; }

    /// <summary>What it is compared as: folded, lowercased, trimmed.</summary>
    public string ComparisonKey { get; }

    /// <summary>The name read into its thing and the words about it, once however often it is compared; a rename is a new name, so it is never stale.</summary>
    public IngredientReading Reading => reading ??= IngredientWords.Read(Value);

    /// <summary>Names are equal when they say the same; the cached reading is no part of what a name is.</summary>
    public bool Equals(ItemName? other) =>
        other is not null && Value == other.Value && ComparisonKey == other.ComparisonKey;

    /// <summary>A hash of what <see cref="Equals(ItemName?)"/> compares.</summary>
    public override int GetHashCode() => HashCode.Combine(Value, ComparisonKey);

    /// <summary>Reads a name, or says why it is not one.</summary>
    /// <param name="value">What the person typed.</param>
    public static Result<ItemName> Create(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return ShoppingErrors.NameRequired;
        }

        return trimmed.Length > MaxLength
            ? ShoppingErrors.NameTooLong
            : new ItemName(trimmed, Fold(trimmed));
    }

    /// <summary>
    /// The comparison form. German umlauts are expanded (<c>ü</c> to <c>ue</c>) so "Müsli" meets "Muesli";
    /// other diacritics are dropped.
    /// </summary>
    public static string Fold(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var expanded = new StringBuilder(value.Length + 4);

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            expanded.Append(character switch
            {
                'ä' => "ae",
                'ö' => "oe",
                'ü' => "ue",
                'ß' => "ss",
                _ => character.ToString()
            });
        }

        // InvariantGlobalization makes String.Normalize a no-op, so marks are dropped by hand.
        var folded = new StringBuilder(expanded.Length);

        foreach (var character in expanded.ToString())
        {
            folded.Append(Simplify(character));
        }

        return folded.ToString();
    }

    private static char Simplify(char character) => character switch
    {
        'á' or 'à' or 'â' or 'ã' or 'å' => 'a',
        'é' or 'è' or 'ê' or 'ë' => 'e',
        'í' or 'ì' or 'î' or 'ï' => 'i',
        'ó' or 'ò' or 'ô' or 'õ' => 'o',
        'ú' or 'ù' or 'û' => 'u',
        'ç' => 'c',
        'ñ' => 'n',
        _ => character
    };

    /// <summary>The name, as written.</summary>
    public override string ToString() => Value;
}
