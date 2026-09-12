namespace Api.Infrastructure;

/// <summary>
/// The query parameters one endpoint accepts, attached as endpoint metadata.
/// </summary>
/// <remarks>
/// Declared once and used twice: the guard middleware rejects anything not
/// listed here, and the OpenAPI document describes exactly these — so the
/// generated client can only send what the server will accept.
/// </remarks>
/// <param name="single">Parameters that may appear at most once.</param>
/// <param name="repeatable">
/// Parameters that may appear several times, because repeating them means
/// something — <c>?tag=vegan&amp;tag=quick</c> asks for both.
/// </param>
/// <param name="integers">
/// Which of them are whole numbers. A query string is text either way; this
/// only tells the contract, so a client sends <c>20</c> rather than
/// <c>"20"</c>.
/// </param>
internal sealed class AllowedQueryParameters(
    IReadOnlyCollection<string> single,
    IReadOnlyCollection<string> repeatable,
    IReadOnlyCollection<string>? integers = null)
{
    private readonly HashSet<string> all = [.. single, .. repeatable];

    private readonly HashSet<string> repeatableNames = [.. repeatable];

    private readonly HashSet<string> integerNames = [.. integers ?? []];

    internal IReadOnlyCollection<string> Single { get; } = single;

    internal IReadOnlyCollection<string> Repeatable { get; } = repeatable;

    internal bool Contains(string name) => all.Contains(name);

    internal bool IsRepeatable(string name) => repeatableNames.Contains(name);

    internal bool IsInteger(string name) => integerNames.Contains(name);
}
