using System.Globalization;
using System.Text.RegularExpressions;
using Domain.Recipes;

namespace Domain.Import;

/// <summary>Reads what a recipe says it makes — "4 Portionen", "12 Stück", "1 Kuchen (26 cm)", "Makes 24 cookies" — into a <see cref="Yield"/>.</summary>
/// <remarks>
/// German and English, any case. A number alone, or beside a word for people, is servings; beside a
/// word for things it is pieces and the word is kept as the label. Anything else is read as the
/// number it carries, as imports always have, and text with no number is left alone: a made-up
/// yield scales every amount by a lie.
/// </remarks>
public static partial class YieldText
{
    // Words for people: the yield is portions, which need no label.
    private static readonly HashSet<string> ServingWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "portion", "portionen", "person", "personen", "pers", "serving", "servings", "serves", "people", "persons"
    };

    // Words that already mean "a piece": counted, but no label worth keeping.
    private static readonly HashSet<string> PlainPieceWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "stück", "stücke", "stk", "piece", "pieces", "pcs"
    };

    // Words for things: counted as pieces and kept, as written, as the label.
    private static readonly HashSet<string> PieceWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "plätzchen", "kekse", "cookie", "cookies", "muffin", "muffins", "cupcake", "cupcakes", "brownies",
        "brötchen", "roll", "rolls", "bun", "buns", "waffel", "waffeln", "waffle", "waffles",
        "pfannkuchen", "pancake", "pancakes", "scheiben", "slices", "riegel", "bar", "bars", "squares",
        "kuchen", "torte", "cake", "cakes", "brot", "brote", "laib", "laibe", "loaf", "loaves",
        "blech", "bleche", "tray", "trays", "glas", "gläser", "jar", "jars", "pizza", "pizzen", "pizzas"
    };

    /// <summary>What the first of these says it makes, preferring one that names what it is: <c>["12", "12 muffins"]</c> is muffins.</summary>
    public static Yield? Read(IEnumerable<string> written)
    {
        ArgumentNullException.ThrowIfNull(written);

        var readings = written.Select(Parse).OfType<Reading>().ToList();

        return (readings.Find(reading => reading.Named) ?? readings.FirstOrDefault())?.Yield;
    }

    /// <summary>What this says it makes, or null when it names no number.</summary>
    public static Yield? Read(string? written) => Parse(written)?.Yield;

    /// <summary>
    /// What a number and a word, given apart (as a model gives them), count: <c>12</c> and "Muffins" is pieces,
    /// <c>4</c> and "Portionen", or no word at all, servings.
    /// </summary>
    public static YieldKind KindOf(decimal? amount, string? label) =>
        Read(string.Create(CultureInfo.InvariantCulture, $"{amount ?? 1} {label}")) is { Kind: YieldKind.Pieces }
            ? YieldKind.Pieces
            : YieldKind.Servings;

    private static Reading? Parse(string? written)
    {
        var match = written is null ? null : Pattern().Match(written.Trim());

        if (match is not { Success: true })
        {
            return null;
        }

        // A range: "6-8 servings" makes 8. Per-portion figures divide by this, and Culina's nutrition
        // is a lower bound, so the larger number keeps the per-portion figure from being inflated.
        // Scaling is unaffected: amounts stay as written and only the label moves.
        var amount = Number(match.Groups["to"].Success ? match.Groups["to"].Value : match.Groups["from"].Value);
        var rest = match.Groups["rest"].Value.Trim();
        var word = Word().Match(rest).Value;

        var (kind, label, named) = word switch
        {
            _ when ServingWords.Contains(word) => (YieldKind.Servings, null, true),
            _ when PlainPieceWords.Contains(word) => (YieldKind.Pieces, null, true),
            _ when PieceWords.Contains(word) => (YieldKind.Pieces, Label(rest, word), true),
            _ => (YieldKind.Servings, null, false)
        };

        return Yield.Create(amount, kind, label).Match(
            yield => new Reading(yield, named),
            _ => (Reading?)null);
    }

    // As written ("Kuchen (26 cm)"), unless that is more than a label holds: then just the word.
    private static string Label(string rest, string word) =>
        rest.Length <= Yield.MaxLabelLength ? rest : word;

    private static decimal Number(string text) =>
        decimal.Parse(text.Replace(',', '.'), CultureInfo.InvariantCulture);

    private sealed record Reading(Yield Yield, bool Named);

    // Anything before the first digit ("für", "Serves", "Makes about"), the number or range, then the rest.
    [GeneratedRegex(@"^\D*?(?<from>\d+(?:[.,]\d+)?)(?:\s*(?:-|–|—|to|bis)\s*(?<to>\d+(?:[.,]\d+)?))?\s*(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Pattern();

    [GeneratedRegex(@"^[\p{L}]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Word();
}
