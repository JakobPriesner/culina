using System.Diagnostics;
using Domain.Shared;

namespace Application.Telemetry;

/// <summary>
/// One span and one duration measurement for a command or query handler.
/// </summary>
/// <remarks>
/// <para>
/// Automatic instrumentation sees the HTTP request and the SQL, but not the use
/// case in between — which is the layer an operator actually asks about. Every
/// handler opens one of these:
/// </para>
/// <code>
/// using var tracked = UseCaseActivity.Start("Recipes.Create");
/// // ...
/// return tracked.Record(result);
/// </code>
/// <para>
/// It lives here, in the handler's own layer, rather than in a decorator: the
/// behaviour is then visible where it happens instead of being resolved by
/// reflection somewhere else.
/// </para>
/// </remarks>
public sealed class UseCaseActivity : IDisposable
{
    private readonly string useCase;
    private readonly Activity? activity;
    private readonly long startedAt;
    private string outcome = "success";

    private UseCaseActivity(string useCase)
    {
        this.useCase = useCase;
        // Span name is <Domain>.<Operation> — the folder path, so a span points
        // straight at the code that produced it.
        activity = CulinaTelemetry.ActivitySource.StartActivity(useCase);
        startedAt = Stopwatch.GetTimestamp();
    }

    /// <summary>Starts tracking a use case.</summary>
    /// <param name="useCase">
    /// <c>&lt;Domain&gt;.&lt;Operation&gt;</c>, matching the folder the handler
    /// lives in.
    /// </param>
    public static UseCaseActivity Start(string useCase) => new(useCase);

    /// <summary>Adds a low-cardinality tag to the span.</summary>
    /// <param name="key">The tag name, prefixed <c>culina.</c>.</param>
    /// <param name="value">
    /// An id or an enum value. Never free text, an email or a search query:
    /// high-cardinality tags are what make a tracing backend fall over.
    /// </param>
    public void Tag(string key, object? value) => activity?.SetTag(key, value);

    /// <summary>Records the outcome and returns the result unchanged.</summary>
    /// <param name="result">The handler's outcome.</param>
    public Result Record(Result result)
    {
        result.Match(() => { }, Failed);

        return result;
    }

    /// <summary>Records the outcome and returns the result unchanged.</summary>
    /// <typeparam name="TValue">What the handler produced.</typeparam>
    /// <param name="result">The handler's outcome.</param>
    public Result<TValue> Record<TValue>(Result<TValue> result)
    {
        result.Match(_ => { }, Failed);

        return result;
    }

    /// <summary>Stops the span and records the duration.</summary>
    public void Dispose()
    {
        CulinaTelemetry.UseCaseDuration.Record(
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            new KeyValuePair<string, object?>("usecase", useCase),
            new KeyValuePair<string, object?>("outcome", outcome));

        activity?.Dispose();
    }

    private void Failed(Error error)
    {
        // The error code, never the description: the description is prose and
        // will be reworded, and a code keeps the metric's cardinality bounded.
        outcome = error.Code;
        activity?.SetStatus(ActivityStatusCode.Error, error.Code);
    }
}
