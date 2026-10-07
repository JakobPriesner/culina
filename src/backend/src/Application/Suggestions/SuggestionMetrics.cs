using System.Diagnostics.Metrics;
using Application.Abstractions;
using Application.Telemetry;

namespace Application.Suggestions;

/// <summary>
/// What an operator can see about suggestions. No per-request log line (every page load), and nothing identifying:
/// tags are bounded enumerations, so cardinality stays flat and the household's cooking stays out of the collector.
/// </summary>
internal static class SuggestionMetrics
{
    // How many came back against how many were asked for. Not an error to be short, but a histogram drifting to zero means a filter has become too strict.
    private static readonly Histogram<int> ReturnedCount = CulinaTelemetry.Meter.CreateHistogram<int>(
        "culina.suggestions.returned",
        unit: "{recipe}",
        description: "How many suggestions one request produced.");

    // Suggestions somebody hid: the loudest signal, because it costs effort.
    private static readonly Counter<long> Dismissals = CulinaTelemetry.Meter.CreateCounter<long>(
        "culina.suggestions.dismissed",
        description: "Recipes a person hid from their own suggestions.");

    // Dismissals taken back: how a too-eager gesture shows up.
    private static readonly Counter<long> Restorations = CulinaTelemetry.Meter.CreateCounter<long>(
        "culina.suggestions.restored",
        description: "Dismissals undone.");

    internal static void Returned(int count, SuggestionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ReturnedCount.Record(
            count,
            new KeyValuePair<string, object?>("purpose", context.Purpose.ToString()),
            // Whether the request was satisfiable at all: separates "small kitchen" from "filtering too hard".
            new KeyValuePair<string, object?>("short", count < context.Count));
    }

    internal static void Dismissed() => Dismissals.Add(1);

    internal static void Restored() => Restorations.Add(1);
}
