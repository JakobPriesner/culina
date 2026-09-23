using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Domain.Import;

namespace Infrastructure.Import.Tandoor;

/// <summary>
/// A step's ingredient row, and where it ended up in the recipe's own list.
/// </summary>
/// <param name="Line">The row as Tandoor sent it.</param>
/// <param name="Position">
/// Its index in <see cref="SourceRecipe.Ingredients"/>, or null when it never
/// became an ingredient — a header row, or a row with nothing in it.
/// </param>
internal sealed record TandoorStepIngredient(TandoorIngredient Line, int? Position);

/// <summary>
/// Reads the template language Tandoor allows inside a step's instruction.
/// </summary>
/// <remarks>
/// <para>
/// Tandoor renders a step through Jinja2 with one variable in scope,
/// <c>ingredients</c>, so that a cook can write "stir in {{ ingredients[0] }}"
/// and have the amount move when they change the servings. Left alone, that
/// text imports as the braces themselves, which is both wrong and ugly — the
/// one place the import visibly produces something nobody wrote.
/// </para>
/// <para>
/// This is not a Jinja2 engine and must not become one. Tandoor's own
/// documentation describes exactly two forms — the whole ingredient and one of
/// its four fields — and those, plus the undocumented <c>scale()</c>, are what
/// a recipe actually contains. Everything else is stripped: the point of the
/// syntax is that it disappears when rendered, so leaving <c>{% if %}</c> in a
/// cook's instructions would be the one outcome nobody wants.
/// </para>
/// <para>
/// The whole-ingredient form becomes a real reference rather than words. That
/// is the entire reason the author wrote it: this app scales a step's amounts
/// through <see cref="SourceIngredientReference"/> by precisely the mechanism
/// Tandoor scales them through <c>ingredients[n]</c>, so the recipe keeps
/// working instead of being frozen at whatever the servings were on the day it
/// came over. A field on its own has no such equivalent — <c>.food</c> and
/// <c>.note</c> do not scale, and an <c>.amount</c> with no ingredient around
/// it is a number this app has nowhere to hang — so those become the words
/// Tandoor would have shown.
/// </para>
/// <para>
/// The index is the step's own and starts at zero, and it counts every row
/// Tandoor sent, including the header rows that are not ingredients. That is
/// what <c>step.ingredients.all()</c> is over there, and an off-by-one here
/// would quietly put the wrong amount in the sentence.
/// </para>
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
                // An unclosed tag is not a tag. Tandoor would refuse the whole
                // instruction here; keeping the characters as words loses
                // nothing and costs the cook nothing.
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
    /// The instruction's line breaks, as this app would rather store them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A step's instruction is Markdown over there, and Tandoor renders it with
    /// the extension that turns a single newline into a line break. So the
    /// newlines in the text are the line breaks a cook sees, and they are kept
    /// — a step that was three lines of "bake, rest, slice" must not arrive as
    /// one paragraph.
    /// </para>
    /// <para>
    /// Kept, but tidied. Carriage returns are somebody's editor rather than
    /// anything they meant, trailing spaces are the other way of asking for a
    /// line break and are now redundant, and a run of blank lines renders over
    /// there as the single gap it is written here as.
    /// </para>
    /// </remarks>
    private static string Lines(string instruction) =>
        Blanks().Replace(
            Trailing().Replace(Unmarked(instruction.ReplaceLineEndings("\n")), string.Empty),
            "\n\n");

    /// <summary>
    /// The words Markdown was decorating, without the decoration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tandoor renders the instruction as Markdown; a step here is plain text
    /// and a reference, with nowhere to put emphasis. So the choice is the
    /// words or the punctuation, and a step that reads "**Tipp:** ..." on the
    /// other side arrived here with the asterisks in it.
    /// </para>
    /// <para>
    /// Deliberately timid, because a recipe is full of characters that only
    /// look like Markdown. Emphasis is unwrapped only where a marker actually
    /// closes — so <c>2 * 3</c> and <c>creme_fraiche</c> keep their
    /// punctuation, and so does a lone asterisk somebody left behind. List
    /// markers stay: a dash at the start of a line reads as the list it was.
    /// </para>
    /// </remarks>
    private static string Unmarked(string instruction) =>
        Heading().Replace(
            Code().Replace(
                Emphasis().Replace(Strong().Replace(instruction, "$2"), "${text}"),
                "$1"),
            string.Empty);

    /// <summary>Where a tag starts here, and what closes it.</summary>
    private static (int Start, string Closing)? Opening(string instruction, int index)
    {
        var rest = instruction.AsSpan(index);

        if (rest.StartsWith("{{", StringComparison.Ordinal))
        {
            return (index + 2, "}}");
        }

        // A statement never produces text on its own, but it has to be
        // recognised to be removed, and its closing brace differs.
        return rest.StartsWith("{%", StringComparison.Ordinal) ? (index + 2, "%}") : null;
    }

    /// <summary>
    /// What one tag comes to: a reference, some words, or nothing at all.
    /// </summary>
    /// <remarks>
    /// Nothing at all is the right answer far more often than it looks. An
    /// index past the end of the list renders as empty in Jinja2 too, so a
    /// reference to an ingredient that was deleted over there disappears here
    /// exactly as it does there.
    /// </remarks>
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
            // The whole ingredient, which is the form worth keeping as a
            // reference. A row that never became one — a header — has only its
            // words to offer.
            return ingredient.Position is { } place
                ? new SourceIngredientReference(place)
                : Words(Whole(ingredient.Line));
        }

        return Words(Field(ingredient.Line, field));
    }

    /// <summary>
    /// An ingredient written out, in the order Tandoor writes it.
    /// </summary>
    /// <remarks>The note is not part of it, over there or here.</remarks>
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

    /// <summary>
    /// A food or a unit, singular or plural as Tandoor would have shown it.
    /// </summary>
    /// <remarks>
    /// Tandoor decides by the amount as written, not as scaled, and never
    /// pluralises a row it was told has no amount. Worth copying exactly: this
    /// is text now, so whatever it says is what it will say forever.
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

    /// <summary>Spaces and tabs left at the end of a line.</summary>
    [GeneratedRegex(@"[ \t]+(?=\n)|[ \t]+$", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex Trailing();

    /// <summary>More blank lines in a row than any gap is.</summary>
    [GeneratedRegex(@"\n{3,}", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex Blanks();

    /// <summary>
    /// <c>**bold**</c> or <c>__bold__</c>, wrapping something, on one line.
    /// </summary>
    [GeneratedRegex(@"(\*\*|__)(?=\S)(.+?)(?<=\S)\1", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex Strong();

    /// <summary>
    /// <c>*italic*</c> or <c>_italic_</c>, with the underscore form refused
    /// inside a word so that <c>creme_fraiche</c> survives.
    /// </summary>
    /// <remarks>
    /// Both arms name the same group, which .NET allows, so one replacement
    /// works whichever marker matched.
    /// </remarks>
    [GeneratedRegex(
        @"(?<![\w*])\*(?=\S)(?<text>[^*\n]+?)(?<=\S)\*(?![\w*])"
        + @"|(?<![\w_])_(?=\S)(?<text>[^_\n]+?)(?<=\S)_(?![\w_])",
        RegexOptions.None,
        matchTimeoutMilliseconds: 500)]
    private static partial Regex Emphasis();

    /// <summary>A span of inline code, which here is just words.</summary>
    [GeneratedRegex(@"`([^`\n]+)`", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex Code();

    /// <summary>The hashes that open an ATX heading, and the space after them.</summary>
    [GeneratedRegex(@"^[ \t]{0,3}#{1,6}[ \t]+", RegexOptions.Multiline, matchTimeoutMilliseconds: 500)]
    private static partial Regex Heading();

    /// <summary>
    /// <c>ingredients[3]</c>, on its own or with one of its four fields.
    /// </summary>
    [GeneratedRegex(
        @"^ingredients\s*\[\s*(\d{1,4})\s*\]\s*(?:\.\s*(amount|unit|food|note))?$",
        RegexOptions.None,
        matchTimeoutMilliseconds: 200)]
    private static partial Regex Reference();

    /// <summary>
    /// <c>scale(200)</c>: a number that moves with the servings, which this app
    /// has no way to store on its own, so the number is kept as it was written.
    /// </summary>
    [GeneratedRegex(
        @"^scale\s*\(\s*(\d+(?:\.\d+)?)\s*\)$",
        RegexOptions.None,
        matchTimeoutMilliseconds: 200)]
    private static partial Regex Scaled();
}
