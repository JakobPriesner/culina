using System.Collections;

namespace Api.Infrastructure;

/// <summary>
/// Named values for a logging scope that also read well as text.
/// </summary>
/// <remarks>
/// A console formatter writes each scope's <c>ToString()</c> beside its
/// values. A dictionary's is its type name, which put
/// <c>System.Collections.Generic.Dictionary`2[...]</c> on every production
/// line; this one is <c>TraceId:… ClientAddress:…</c>.
/// </remarks>
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
