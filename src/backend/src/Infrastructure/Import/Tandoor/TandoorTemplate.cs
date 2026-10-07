using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Domain.Import;

namespace Infrastructure.Import.Tandoor;

/// <summary>
/// A step's ingredient row, and its index in <see cref="SourceRecipe.Ingredients"/> (null for a
/// header or empty row).
/// </summary>
/// <param name="Line">The row as Tandoor sent it.</param>
/// <param name="Position">Index in the recipe's ingredient list, or null.</param>
internal sealed record TandoorStepIngredient(TandoorIngredient Line, int? Position);

/// <summary>Reads the Jinja2-style templates Tandoor allows inside a step's instruction.</summary>
/// <remarks>
/// Not a Jinja2 engine and must not become one: only <c>ingredients[n]</c> (and its four fields)
/// and <c>scale()</c> are resolved, everything else is stripped. The whole-ingredient form becomes
/// a real <see cref="SourceIngredientReference"/> so amounts keep scaling; fields become words. The
/// index is the step's own, zero-based, and counts header rows, as <c>step.ingredients.all()</c>
/// does in Tandoor.
/// </remarks>
internal static partial class TandoorTemplate
{
    /// <summary>What a step says, with its templates resolved.</summary>
    /// <param name="instruction">The instruction as Tandoor stores it.</param>
    /// <param name="ingredients">The step's ingredient rows, in Tandoor's order.</param>
    internal static IReadOnlyList<SourceStepSegment> Read(
        string? instruction,
        IReadOnlyList<TandoorStepIngredient> ingredients)
    {
        ArgumentNullException.ThrowIfNull(ingredients);

        if (string.IsNullOrEmpty(instruction))
        {
            return [];
        }

        instruction = Lines(instruction);

        List<SourceStepSegment> segments = [];
        var words = new StringBuilder();
        var index = 0;

        while (index < instruction.Length)
        {
            var open = Opening(instruction, index);

            if (open is null)
            {
                words.Append(instruction[index]);
                index++;

                continue;
            }

            var (start, closing) = open.Value;
            var end = instruction.IndexOf(closing, start, StringComparison.Ordinal);

            if (end < 0)
            {
                // An unclosed tag is not a tag: keep the characters as words.
                words.Append(instruction[index]);
                index++;

                continue;
            }

            var resolved = Resolve(instruction[start..end], ingredients);

            if (resolved is SourceTextSegment text)
            {
                words.Append(text.Value);
            }
            else if (resolved is not null)
            {
                Flush(segments, words);
                segments.Add(resolved);
            }

            index = end + closing.Length;
        }

        Flush(segments, words);

        return segments;
    }

    /// <summary>
    /// The instruction's line breaks, tidied: Tandoor renders single newlines as breaks, so they
    /// are kept.
    /// </summary>
    private static string Lines(string instruction) =>
        Blanks().Replace(
            Trailing().Replace(Unmarked(instruction.ReplaceLineEndings("\n")), string.Empty),
            "\n\n");

    /// <summary>The words Markdown was decorating, without the decoration.</summary>
    /// <remarks>
    /// Deliberately timid: emphasis is unwrapped only where a marker closes, so <c>2 * 3</c> and
    /// <c>creme_fraiche</c> keep their punctuation. List markers stay.
    /// </remarks>
    private static string Unmarked(string instruction) =>
        Heading().Replace(
            Code().Replace(
                Emphasis().Replace(Strong().Replace(instruction, "$2"), "${text}"),
                "$1"),
            string.Empty);

    private static (int Start, string Closing)? Opening(string instruction, int index)
    {
        var rest = instruction.AsSpan(index);

        if (rest.StartsWith("{{", StringComparison.Ordinal))
        {
            return (index + 2, "}}");
        }

        // A statement never produces text, but it must be recognised to be removed.
        return rest.StartsWith("{%", StringComparison.Ordinal) ? (index + 2, "%}") : null;
    }

    /// <summary>
    /// What one tag comes to: a reference, some words, or nothing (as in Jinja2 for an out-of-range
    /// index).
    /// </summary>
    private static SourceStepSegment? Resolve(
        string expression,
        IReadOnlyList<TandoorStepIngredient> ingredients)
    {
        var trimmed = expression.Trim();

        if (Scaled().Match(trimmed) is { Success: true } scaled)
        {
            return Words(scaled.Groups[1].Value);
        }

        var match = Reference().Match(trimmed);

        if (!match.Success
            || !int.TryParse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture, out var position)
            || position >= ingredients.Count)
        {
            return null;
        }

        var ingredient = ingredients[position];
        var field = match.Groups[2].Value;

        if (field.Length == 0)
        {
            // The whole ingredient stays a reference; a header row has only its words.
            return ingredient.Position is { } place
                ? new SourceIngredientReference(place)
                : Words(Whole(ingredient.Line));
        }

        return Words(Field(ingredient.Line, field));
    }

    /// <summary>An ingredient written out in Tandoor's order; the note is not part of it.</summary>
    private static string Whole(TandoorIngredient line) =>
        string.Join(
            ' ',
            new[] { Amount(line), Named(line.Unit, line), Named(line.Food, line) }
                .Where(part => part.Length > 0));

    private static string Field(TandoorIngredient line, string field) => field switch
    {
        "amount" => Amount(line),
        "unit" => Named(line.Unit, line),
        "food" => Named(line.Food, line),
        "note" => line.Note?.Trim() ?? string.Empty,
        _ => string.Empty
    };

    /// <summary>A food or unit, singular or plural as Tandoor shows it.</summary>
    /// <remarks>
    /// Tandoor decides by the amount as written, not as scaled, and never pluralises a row with no
    /// amount.
    /// </remarks>
    private static string Named(TandoorNamed? named, TandoorIngredient line)
    {
        var singular = named?.Name?.Trim() ?? string.Empty;
        var plural = named?.PluralName?.Trim();

        return line.NoAmount || string.IsNullOrEmpty(plural) || line.Amount == 1m
            ? singular
            : plural;
    }

    private static string Amount(TandoorIngredient line) =>
        line.NoAmount || line.Amount is not > 0m
            ? string.Empty
            : line.Amount.Value.ToString("0.####", CultureInfo.InvariantCulture);

    private static SourceTextSegment? Words(string value) =>
        value.Length == 0 ? null : new SourceTextSegment(value);

    private static void Flush(List<SourceStepSegment> segments, StringBuilder words)
    {
        if (words.Length == 0)
        {
            return;
        }

        segments.Add(new SourceTextSegment(words.ToString()));
        words.Clear();
    }

    [GeneratedRegex(@"[ \t]+(?=\n)|[ \t]+$", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex Trailing();

    [GeneratedRegex(@"\n{3,}", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex Blanks();

    [GeneratedRegex(@"(\*\*|__)(?=\S)(.+?)(?<=\S)\1", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex Strong();

    /// <remarks>
    /// Both arms name the same group, so one replacement works for either marker.
    /// </remarks>
    [GeneratedRegex(
        @"(?<![\w*])\*(?=\S)(?<text>[^*\n]+?)(?<=\S)\*(?![\w*])"
        + @"|(?<![\w_])_(?=\S)(?<text>[^_\n]+?)(?<=\S)_(?![\w_])",
        RegexOptions.None,
        matchTimeoutMilliseconds: 500)]
    private static partial Regex Emphasis();

    [GeneratedRegex(@"`([^`\n]+)`", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex Code();

    [GeneratedRegex(@"^[ \t]{0,3}#{1,6}[ \t]+", RegexOptions.Multiline, matchTimeoutMilliseconds: 500)]
    private static partial Regex Heading();

    [GeneratedRegex(
        @"^ingredients\s*\[\s*(\d{1,4})\s*\]\s*(?:\.\s*(amount|unit|food|note))?$",
        RegexOptions.None,
        matchTimeoutMilliseconds: 200)]
    private static partial Regex Reference();

    /// <summary>
    /// <c>scale(200)</c>: kept as the number written, since this app cannot store a serving-scaled
    /// number.
    /// </summary>
    [GeneratedRegex(
        @"^scale\s*\(\s*(\d+(?:\.\d+)?)\s*\)$",
        RegexOptions.None,
        matchTimeoutMilliseconds: 200)]
    private static partial Regex Scaled();
}
