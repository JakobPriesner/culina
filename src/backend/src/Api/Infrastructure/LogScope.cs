using System.Collections;

namespace Api.Infrastructure;

/// <summary>Named logging-scope values whose <c>ToString()</c> reads as <c>Key:Value</c> pairs, not a type name.</summary>
/// <param name="values">The names and values, in the order they are printed.</param>
internal sealed class LogScope(params KeyValuePair<string, object?>[] values)
    : IReadOnlyList<KeyValuePair<string, object?>>
{
    public int Count => values.Length;

    public KeyValuePair<string, object?> this[int index] => values[index];

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
        ((IEnumerable<KeyValuePair<string, object?>>)values).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() =>
        string.Join(' ', values.Select(value => $"{value.Key}:{value.Value}"));
}
