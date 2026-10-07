using System.Buffers;
using System.Text;
using System.Text.Json;
using Application.Abstractions;

namespace Infrastructure.Assistance;

/// <summary>
/// Reads a <see cref="DraftedRecipe"/> out of streamed JSON that has not finished arriving.
/// </summary>
/// <remarks>
/// <see cref="Read"/> cuts the text back to the last finished value and closes the open containers,
/// so a half-typed ingredient is absent rather than half-present.
/// </remarks>
internal sealed class PartialRecipe
{
    private readonly StringBuilder written = new();

    // Compared as repaired text: drafts hold lists, which would compare by reference.
    private string lastRead = string.Empty;

    /// <summary>Whether a <c>, } ]</c> has arrived since the last read; rescanning per token is quadratic.</summary>
    private bool cutPointArrived;

    /// <summary>Everything the model has written so far.</summary>
    internal string Text => written.ToString();

    /// <summary>Adds what just arrived.</summary>
    /// <param name="text">The delta, which may be a single character.</param>
    internal void Add(string? text)
    {
        if (text is { Length: > 0 })
        {
            written.Append(text);
            cutPointArrived |= text.AsSpan().IndexOfAny(CutPointCharacters) >= 0;
        }
    }

    /// <summary>The recipe so far, or null when it is not readable yet or unchanged.</summary>
    internal DraftedRecipe? Read()
    {
        if (!cutPointArrived)
        {
            return null;
        }

        cutPointArrived = false;

        if (Closed(written.ToString()) is not { } repaired || repaired == lastRead)
        {
            return null;
        }

        lastRead = repaired;

        return Parse(repaired);
    }

    /// <summary>The recipe so far, changed or not; for salvaging a stream that ended badly. Never null.</summary>
    internal DraftedRecipe SoFar() =>
        (Closed(written.ToString()) is { } repaired ? Parse(repaired) : null) ?? new DraftedRecipe();

    /// <summary>The whole answer; strict, so an unparseable finished answer is not quietly repaired.</summary>
    internal DraftedRecipe? ReadWhole() => Parse(written.ToString());

    private static readonly SearchValues<char> CutPointCharacters = SearchValues.Create(",}]");

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

    // The longest prefix ending where a value just finished (a comma outside a string, or after a
    // closing brace/bracket), with its containers shut. Null before the first such point.
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
                        // Unbalanced: not the shape we asked for, so not ours to repair.
                        return null;
                    }

                    open.Pop();
                    cut = at + 1;
                    shut = Shut(open);
                    break;
                case ',':
                    // The comma is left out so the prefix never waits on a value.
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
