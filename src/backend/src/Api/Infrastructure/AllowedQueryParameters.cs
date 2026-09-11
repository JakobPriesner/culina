namespace Api.Infrastructure;

/// <summary>
/// The query parameters one endpoint accepts, attached as endpoint metadata.
/// </summary>
/// <param name="single">Parameters that may appear at most once.</param>
/// <param name="repeatable">
/// Parameters that may appear several times, because repeating them means
/// something — <c>?tag=vegan&amp;tag=quick</c> asks for both.
/// </param>
internal sealed class AllowedQueryParameters(
    IReadOnlyCollection<string> single,
    IReadOnlyCollection<string> repeatable)
{
    private readonly HashSet<string> all =
        [.. single, .. repeatable];

    private readonly HashSet<string> repeatable =
        [.. repeatable];

    internal bool Contains(string name) => all.Contains(name);

    internal bool IsRepeatable(string name) => repeatable.Contains(name);
}
