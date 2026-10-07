using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.Fixtures;

/// <summary>One line the host logged, with its structured fields and scopes.</summary>
/// <param name="Category">The logger category.</param>
/// <param name="Level">The level it was written at.</param>
/// <param name="EventId">The event id, which is what an alert keys on.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="Fields">The message template's named values.</param>
/// <param name="Scopes">Every value on the scopes active when it was written.</param>
/// <param name="Exception">The attached exception as text, or null.</param>
public sealed record LoggedLine(
    string Category,
    LogLevel Level,
    int EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Fields,
    IReadOnlyDictionary<string, object?> Scopes,
    string? Exception)
{
    /// <summary>A field or scope value as text, or null when the line has neither.</summary>
    public string? this[string name] =>
        Fields.TryGetValue(name, out var field) ? Convert.ToString(field, System.Globalization.CultureInfo.InvariantCulture)
        : Scopes.TryGetValue(name, out var scope) ? Convert.ToString(scope, System.Globalization.CultureInfo.InvariantCulture)
        : null;
}

/// <summary>Keeps every line the host logs, after level filters, so a test can say what an operator would read.</summary>
public sealed class RecordingLogs : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<LoggedLine> lines = new();
    private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();

    /// <summary>Everything logged so far, oldest first.</summary>
    public IReadOnlyList<LoggedLine> Lines => [.. lines];

    /// <summary>The first line that matches, waiting briefly: the request line can be written after the response.</summary>
    public async Task<LoggedLine?> WaitForAsync(Func<LoggedLine, bool> match)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (lines.FirstOrDefault(match) is { } found)
            {
                return found;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return null;
    }

    public ILogger CreateLogger(string categoryName) => new Recorder(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class Recorder(string category, RecordingLogs owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => owner.scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scoped = new Dictionary<string, object?>();

            owner.scopes.ForEachScope(
                (scope, collected) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
                    {
                        foreach (var (key, value) in pairs)
                        {
                            collected[key] = value;
                        }
                    }
                    else if (scope is IEnumerable<KeyValuePair<string, object>> objects)
                    {
                        foreach (var (key, value) in objects)
                        {
                            collected[key] = value;
                        }
                    }
                },
                scoped);

            var fields = state is IEnumerable<KeyValuePair<string, object?>> named
                ? named.ToDictionary(pair => pair.Key, pair => pair.Value)
                : [];

            owner.lines.Enqueue(new LoggedLine(
                category,
                logLevel,
                eventId.Id,
                formatter(state, exception),
                fields,
                scoped,
                exception?.ToString()));
        }
    }
}
