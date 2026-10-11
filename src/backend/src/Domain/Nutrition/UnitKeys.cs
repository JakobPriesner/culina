using Domain.Recipes;
using Domain.Shopping;

namespace Domain.Nutrition;

/// <summary>
/// The canonical key of a count unit: every spelling of a clove, a pack or a tablespoon, in German and
/// English, is one key, so a typical weight or a household's own weight is written once.
/// </summary>
/// <remarks>
/// A bare count and <c>piece</c> are both <c>piece</c>. A unit nobody listed (a household's own word,
/// "Handvoll") is its own key: the word, folded. Raise <see cref="NutritionData.Version"/> through
/// <see cref="TypicalWeights.Version"/> when a key changes meaning.
/// </remarks>
public static class UnitKeys
{
    /// <summary>The key of a bare count, and of <c>piece</c>.</summary>
    public const string Piece = "piece";

    private static readonly Dictionary<string, string[]> Spellings = new()
    {
        [Piece] = ["piece", "pieces", "pc", "pcs", "stück", "stücke", "stueck", "stk", "st", "einheit", "einheiten", "unit", "units"],
        ["clove"] = ["clove", "cloves", "zehe", "zehen"],
        ["bunch"] = ["bunch", "bunches", "bund", "bünde", "buende"],
        ["slice"] = ["slice", "slices", "scheibe", "scheiben"],
        ["leaf"] = ["leaf", "leaves", "sheet", "sheets", "blatt", "blätter", "blaetter"],
        ["can"] = ["can", "cans", "tin", "tins", "dose", "dosen"],
        ["pack"] = ["pack", "packs", "packet", "packets", "package", "packages", "sachet", "sachets", "packung", "packungen", "pck", "pckg", "pkg", "päckchen", "paeckchen", "paket", "pakete", "tüte", "tüten", "tuete", "tueten", "tütchen", "beutel"],
        ["cube"] = ["cube", "cubes", "würfel", "wuerfel"],
        ["stick"] = ["stick", "sticks", "stalk", "stalks", "stange", "stangen"],
        ["tub"] = ["tub", "tubs", "becher", "bechern"],
        ["tbsp"] = ["tbsp", "tbsps", "tbs", "tablespoon", "tablespoons", "el", "esslöffel", "essloeffel", "essl"],
        ["tsp"] = ["tsp", "tsps", "teaspoon", "teaspoons", "tl", "teelöffel", "teeloeffel", "teel"],
        ["pinch"] = ["pinch", "pinches", "prise", "prisen"]
    };

    private static readonly Dictionary<string, string> ByWord =
        Spellings.SelectMany(pair => pair.Value.Select(word => (word, key: pair.Key)))
            .ToDictionary(pair => pair.word, pair => pair.key, StringComparer.Ordinal);

    /// <summary>Every canonical key.</summary>
    public static IEnumerable<string> All => Spellings.Keys;

    /// <summary>The key of a unit; <see cref="Piece"/> for none.</summary>
    /// <param name="unit">The unit as the line has it, or null for a bare count.</param>
    public static string Of(Unit? unit) => unit is null ? Piece : Of(unit.Code);

    /// <summary>The key of a unit written as a word: the canonical key, or the word folded.</summary>
    /// <param name="word">The unit as somebody wrote it.</param>
    public static string Of(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        var written = string.Join(' ', word.TrimEnd('.').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var lower = written.ToLowerInvariant();

        return ByWord.TryGetValue(lower, out var key) ? key : ItemName.Fold(written);
    }
}
