using System.Globalization;
using System.Text;
using Domain.Shared;

namespace Domain.Recipes;

/// <summary>Reads and writes the stored form of a step's text.</summary>
/// <remarks>
/// References are inline tokens (<c>[[ingredient:0f1c…]]</c>), not offsets, which rot when a word
/// earlier in the sentence is edited. The token form never leaves the database, so it can change
/// without a new API version.
/// </remarks>
public static class StepText
{
    private const string Open = "[[ingredient:";
    private const string Close = "]]";

    /// <summary>
    /// Written after a literal <c>[</c> that comes before another <c>[</c> or a backslash, so
    /// <c>[[</c> cannot be read back as a reference and a literal <c>[\</c> cannot be read back as
    /// an escape. Rows stored before the backslash was escaped too only ever hold it before <c>[</c>.
    /// </summary>
    private const char Escape = '\\';

    /// <summary>Splits stored text into its segments.</summary>
    public static Result<IReadOnlyList<StepSegment>> Parse(string? stored)
    {
        if (stored is null)
        {
            return RecipeErrors.InvalidStepText;
        }

        List<StepSegment> segments = [];
        var text = new StringBuilder();
        var index = 0;

        while (index < stored.Length)
        {
            if (IsEscaped(stored, index))
            {
                text.Append('[');
                index += 2;

                continue;
            }

            if (!stored.AsSpan(index).StartsWith(Open, StringComparison.Ordinal))
            {
                text.Append(stored[index]);
                index++;

                continue;
            }

            var end = stored.IndexOf(Close, index, StringComparison.Ordinal);

            if (end < 0)
            {
                return RecipeErrors.UnknownIngredientReference;
            }

            var raw = stored[(index + Open.Length)..end];

            if (!Guid.TryParse(raw, CultureInfo.InvariantCulture, out var ingredientId))
            {
                return RecipeErrors.UnknownIngredientReference;
            }

            Flush(segments, text);
            segments.Add(new IngredientSegment(ingredientId));
            index = end + Close.Length;
        }

        Flush(segments, text);

        return segments;
    }

    /// <summary>Writes segments back into the stored form.</summary>
    public static string Serialise(IReadOnlyList<StepSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var stored = new StringBuilder();

        foreach (var segment in segments)
        {
            switch (segment)
            {
                case TextSegment text:
                    AppendEscaped(stored, text.Value);
                    break;

                case IngredientSegment ingredient:
                    stored.Append(Open).Append(ingredient.RecipeIngredientId).Append(Close);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(segments),
                        segment,
                        "Unknown step segment kind.");
            }
        }

        return stored.ToString();
    }

    /// <summary>
    /// Every ingredient a step refers to, for the reference index and for refusing to delete one
    /// still mentioned.
    /// </summary>
    public static IReadOnlySet<Guid> ReferencedIngredients(IReadOnlyList<StepSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        return segments.OfType<IngredientSegment>()
            .Select(segment => segment.RecipeIngredientId)
            .ToHashSet();
    }

    /// <summary>The words alone, for search indexing and plain-text export.</summary>
    public static string PlainText(IReadOnlyList<StepSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        return string.Concat(segments.OfType<TextSegment>().Select(segment => segment.Value));
    }

    private static bool IsEscaped(string stored, int index) =>
        index + 2 < stored.Length
        && stored[index] == '['
        && stored[index + 1] == Escape
        && stored[index + 2] is '[' or Escape;

    private static void AppendEscaped(StringBuilder stored, string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            stored.Append(text[index]);

            if (text[index] == '[' && index + 1 < text.Length && text[index + 1] is '[' or Escape)
            {
                stored.Append(Escape);
            }
        }
    }

    private static void Flush(List<StepSegment> segments, StringBuilder text)
    {
        if (text.Length == 0)
        {
            return;
        }

        segments.Add(new TextSegment(text.ToString()));
        text.Clear();
    }
}
