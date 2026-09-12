using System.Globalization;
using System.Text;
using Domain.Shared;

namespace Domain.Recipes;

/// <summary>
/// Reads and writes the stored form of a step's text.
/// </summary>
/// <remarks>
/// <para>
/// References are stored as inline tokens — <c>[[ingredient:0f1c…]]</c> — and
/// not as character offsets. Offsets rot the moment someone edits a word
/// earlier in the sentence, and a silently wrong offset would attach the wrong
/// amount to the wrong ingredient.
/// </para>
/// <para>
/// The token form never leaves the database: the API exposes segments, so this
/// encoding can change without a new API version.
/// </para>
/// </remarks>
public static class StepText
{
    private const string Open = "[[ingredient:";
    private const string Close = "]]";

    /// <summary>
    /// Escapes a literal <c>[[</c> so text that happens to contain one cannot
    /// be read back as a reference.
    /// </summary>
    private const string EscapedOpen = "[\\[";

    /// <summary>Splits stored text into its segments.</summary>
    /// <param name="stored">The text as it is stored.</param>
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
            if (stored.AsSpan(index).StartsWith(EscapedOpen, StringComparison.Ordinal))
            {
                text.Append("[[");
                index += EscapedOpen.Length;

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
    /// <param name="segments">The segments to store.</param>
    public static string Serialise(IReadOnlyList<StepSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var stored = new StringBuilder();

        foreach (var segment in segments)
        {
            switch (segment)
            {
                case TextSegment text:
                    stored.Append(text.Value.Replace("[[", EscapedOpen, StringComparison.Ordinal));
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
    /// Every ingredient a step refers to, for rebuilding the reference index
    /// and for refusing to delete an ingredient a step still mentions.
    /// </summary>
    /// <param name="segments">The step's segments.</param>
    public static IReadOnlySet<Guid> ReferencedIngredients(IReadOnlyList<StepSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        return segments.OfType<IngredientSegment>()
            .Select(segment => segment.RecipeIngredientId)
            .ToHashSet();
    }

    /// <summary>The words alone, for search indexing and for a plain-text export.</summary>
    /// <param name="segments">The step's segments.</param>
    public static string PlainText(IReadOnlyList<StepSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        return string.Concat(segments.OfType<TextSegment>().Select(segment => segment.Value));
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
