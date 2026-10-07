using System.Diagnostics;
using Domain.Shared;

namespace Application.Telemetry;

/// <summary>One span and one duration measurement for a command or query handler.</summary>
/// <remarks>
/// Automatic instrumentation sees the HTTP request and the SQL, not the use case between them.
/// Every handler opens one (<c>using var tracked = UseCaseActivity.Start("Recipes.Create")</c>,
/// then <c>tracked.Record(result)</c>); it is not a decorator, so the behaviour is visible where it
/// happens.
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
        // Span name is <Domain>.<Operation>, the folder path, so a span points straight at the
        // code.
        activity = CulinaTelemetry.ActivitySource.StartActivity(useCase);
        startedAt = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Starts tracking a use case, named <c>&lt;Domain&gt;.&lt;Operation&gt;</c> after the
    /// handler's folder.
    /// </summary>
    public static UseCaseActivity Start(string useCase) => new(useCase);

    /// <summary>
    /// Adds a low-cardinality tag to the span, key prefixed <c>culina.</c>: an id or enum value,
    /// never free text, an email or a search query.
    /// </summary>
    public void Tag(string key, object? value) => activity?.SetTag(key, value);

    /// <summary>Records the outcome and returns the result unchanged.</summary>
    public Result Record(Result result)
    {
        result.Match(() => { }, Failed);

        return result;
    }

    /// <summary>Records the outcome and returns the result unchanged.</summary>
    public Result<TValue> Record<TValue>(Result<TValue> result)
        where TValue : notnull
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
        // The error code, not the description: prose gets reworded, and a code keeps the metric's
        // cardinality bounded.
        outcome = error.Code;
        activity?.SetStatus(ActivityStatusCode.Error, error.Code);
    }
}
