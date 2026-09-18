using System.Diagnostics.Metrics;
using Application.Abstractions;
using Application.Telemetry;

namespace Application.Suggestions;

/// <summary>
/// What an operator can see about the suggestions.
/// </summary>
/// <remarks>
/// <para>
/// Three measurements, and each one answers a question somebody would actually
/// ask. There is deliberately no per-request log line: a suggestion request
/// happens on every page load, and a log that noisy is one nobody reads.
/// </para>
/// <para>
/// None of this carries a recipe title, an ingredient, a search term or an id.
/// Tags are bounded enumerations, which keeps the cardinality flat and keeps the
/// household's cooking out of whatever the operator's collector writes to.
/// </para>
/// </remarks>
internal static class SuggestionMetrics
{
    /// <summary>
    /// How many came back against how many were asked for.
    /// </summary>
    /// <remarks>
    /// The one number that matters operationally. A ranker returning fewer than
    /// it was asked for is not an error — a household with three recipes has
    /// three suggestions — but a histogram that drifts toward zero is the shape
    /// of a filter that has quietly become too strict, and it is invisible in
    /// every other signal.
    /// </remarks>
    private static readonly Histogram<int> ReturnedCount = CulinaTelemetry.Meter.CreateHistogram<int>(
        "culina.suggestions.returned",
        unit: "{recipe}",
        description: "How many suggestions one request produced.");

    /// <summary>Suggestions somebody hid. The loudest signal, because it costs effort.</summary>
    private static readonly Counter<long> Dismissals = CulinaTelemetry.Meter.CreateCounter<long>(
        "culina.suggestions.dismissed",
        description: "Recipes a person hid from their own suggestions.");

    /// <summary>Dismissals taken back, which is how a too-eager gesture shows up.</summary>
    private static readonly Counter<long> Restorations = CulinaTelemetry.Meter.CreateCounter<long>(
        "culina.suggestions.restored",
        description: "Dismissals undone.");

    internal static void Returned(int count, SuggestionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ReturnedCount.Record(
            count,
            new KeyValuePair<string, object?>("purpose", context.Purpose.ToString()),
            // Whether the request was satisfiable at all, which separates "this
            // kitchen is small" from "something is filtering too hard".
            new KeyValuePair<string, object?>("short", count < context.Count));
    }

    internal static void Dismissed() => Dismissals.Add(1);

    internal static void Restored() => Restorations.Add(1);
}
