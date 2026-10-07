namespace Api.Infrastructure;

/// <summary>The query parameters one endpoint accepts, attached as endpoint metadata.</summary>
/// <remarks>
/// Declared once, used twice: the guard rejects anything unlisted and OpenAPI describes exactly
/// these.
/// </remarks>
/// <param name="single">Parameters that may appear at most once.</param>
/// <param name="repeatable">
/// Parameters that may repeat meaningfully (<c>?tag=vegan&amp;tag=quick</c> asks for both).
/// </param>
/// <param name="integers">
/// Which are whole numbers; only tells the contract, so a client sends <c>20</c> not <c>"20"</c>.
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
