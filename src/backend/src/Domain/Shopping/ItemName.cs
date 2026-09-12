using System.Text;
using Domain.Shared;

namespace Domain.Shopping;

/// <summary>
/// What an item is called, and the form two names are compared in.
/// </summary>
/// <remarks>
/// "Müsli" and "Muesli" are the same thing in a trolley, and so are "Butter"
/// and "butter". The comparison form is folded once and stored alongside the
/// name the person actually wrote, because the list should show their words and
/// merge on ours.
/// </remarks>
public sealed record ItemName
{
    /// <summary>The longest name a shopping list needs.</summary>
    public const int MaxLength = 120;

    private ItemName(string value, string comparisonKey)
    {
        Value = value;
        ComparisonKey = comparisonKey;
    }

    /// <summary>What the person wrote.</summary>
    public string Value { get; }

    /// <summary>What it is compared as: folded, lowercased, trimmed.</summary>
    public string ComparisonKey { get; }

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
    /// The form two names are compared in.
    /// </summary>
    /// <remarks>
    /// German umlauts are expanded rather than stripped: <c>ü</c> becomes
    /// <c>ue</c>, so "Müsli" and "Muesli" meet. Stripping the diacritic would
    /// give "Musli", which meets neither. The rest is folded by decomposing and
    /// dropping the marks, which handles "crème" and "creme".
    /// </remarks>
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

        // InvariantGlobalization makes String.Normalize a no-op, so the marks
        // are dropped by hand for the accents German and English borrow.
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
