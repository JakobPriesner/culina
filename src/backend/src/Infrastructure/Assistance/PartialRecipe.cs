using System.Text;
using System.Text.Json;
using Application.Abstractions;

namespace Infrastructure.Assistance;

/// <summary>
/// A recipe read out of JSON that has not finished arriving.
/// </summary>
/// <remarks>
/// <para>
/// A model holds to the schema it was given, but it writes the answer from the
/// first brace to the last — so for all but the final instant the text on hand
/// is not JSON at all. Showing somebody the recipe being written means reading
/// it anyway, and this is the part that does.
/// </para>
/// <para>
/// It never guesses. <see cref="Read"/> cuts the text back to the last point
/// where a value had definitely finished, closes the containers that were open
/// there, and parses that — so a half-typed ingredient is absent rather than
/// half-present, and no field is ever shown holding a value the model has not
/// actually written. The cost is that the newest line appears a moment after it
/// was written, which is the right way round: a name that flickers as its
/// letters arrive is harder to read than one that simply turns up.
/// </para>
/// <para>
/// Infrastructure rather than application, because what it reads is
/// <see cref="RecipeAnswer"/> — the shape this app asked the model for. The
/// application never sees the JSON, only the drafts that come out of it.
/// </para>
/// </remarks>
internal sealed class PartialRecipe
{
    private readonly StringBuilder written = new();

    /// <summary>
    /// The last text that parsed, so an unchanged draft is not sent twice.
    /// </summary>
    /// <remarks>
    /// Most deltas are a few characters inside a value and move nothing that
    /// can be read yet. Comparing the repaired text is exact and costs a string
    /// compare, where comparing two drafts would compare lists by reference and
    /// call every one of them different.
    /// </remarks>
    private string lastRead = string.Empty;

    /// <summary>Everything the model has written so far.</summary>
    internal string Text => written.ToString();

    /// <summary>Adds what just arrived.</summary>
    /// <param name="text">The delta, which may be a single character.</param>
    internal void Add(string? text)
    {
        if (text is { Length: > 0 })
        {
            written.Append(text);
        }
    }

    /// <summary>
    /// The recipe so far, or null when there is nothing new to show.
    /// </summary>
    /// <remarks>
    /// Null for both "not readable yet" and "readable and unchanged", because
    /// the caller does the same thing with them: send nothing.
    /// </remarks>
    internal DraftedRecipe? Read()
    {
        if (Closed(written.ToString()) is not { } repaired || repaired == lastRead)
        {
            return null;
        }

        lastRead = repaired;

        return Parse(repaired);
    }

    /// <summary>
    /// The recipe so far, whether or not it has changed. Never null.
    /// </summary>
    /// <remarks>
    /// For the end of a stream that ended badly, where the question is not
    /// "what is new" but "what was written before it stopped". A provider that
    /// cut out after the ingredients wrote something worth offering, and the
    /// call was paid for either way.
    /// </remarks>
    internal DraftedRecipe SoFar() =>
        (Closed(written.ToString()) is { } repaired ? Parse(repaired) : null) ?? new DraftedRecipe();

    /// <summary>The whole answer, once it is whole.</summary>
    /// <remarks>
    /// The ordinary parse, not the lenient one: a finished answer that does not
    /// parse is an unusable answer, and quietly repairing it into a recipe with
    /// the last two steps missing would hide that.
    /// </remarks>
    internal DraftedRecipe? ReadWhole() => Parse(written.ToString());

    private static DraftedRecipe? Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<RecipeAnswer>(json, AssistantHttp.Json)?.ToDraft();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The longest prefix of the text that is valid JSON, with its braces shut.
    /// </summary>
    /// <param name="json">Whatever has arrived.</param>
    /// <remarks>
    /// <para>
    /// The cut points are a comma outside a string, and the character after a
    /// <c>}</c> or <c>]</c> that closed one. Both occur only where a value has
    /// just finished, which is what makes the prefix safe to close and parse —
    /// a half-written string, number or keyword is never inside it.
    /// </para>
    /// <para>
    /// Null before the first of them, which is the first field of the recipe.
    /// There is nothing to show until then and nothing worth inventing.
    /// </para>
    /// </remarks>
    private static string? Closed(string json)
    {
        var start = json.IndexOf('{');

        if (start < 0)
        {
            return null;
        }

        var open = new Stack<char>();
        var inString = false;
        var escaped = false;
        var cut = -1;
        var shut = string.Empty;

        for (var at = start; at < json.Length; at++)
        {
            var character = json[at];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                case '[':
                    open.Push(character == '{' ? '}' : ']');
                    break;
                case '}':
                case ']':
                    if (open.Count == 0)
                    {
                        // Not ours to repair: the text is not the shape this
                        // was told to expect.
                        return null;
                    }

                    open.Pop();
                    cut = at + 1;
                    shut = Shut(open);
                    break;
                case ',':
                    // The comma itself is left out, so the prefix never ends
                    // waiting for a value that has not been written.
                    cut = at;
                    shut = Shut(open);
                    break;
                default:
                    break;
            }
        }

        return cut < 0 ? null : json[start..cut] + shut;
    }

    private static string Shut(Stack<char> open) => new([.. open]);
}
