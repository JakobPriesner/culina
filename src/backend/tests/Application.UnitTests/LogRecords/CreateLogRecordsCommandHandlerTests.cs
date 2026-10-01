using Application.LogRecords;
using Application.LogRecords.Create;
using Domain.Shared;
using Microsoft.Extensions.Logging;
using TestSupport;

namespace Application.UnitTests.LogRecords;

/// <summary>
/// What the browser's reports turn into on the server, and what is refused.
/// </summary>
/// <remarks>
/// Anybody can send these, so the properties worth proving are the ones that
/// stop a caller choosing how loud a line is or how much of it there is.
/// </remarks>
public class CreateLogRecordsCommandHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ShouldWriteOneLinePerRecord_InTheWebAppCategory()
    {
        // Arrange
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);

        // Act
        var result = await handler.Handle(
            Command(Record("uncaught_error"), Record("unhandled_rejection")),
            Token);

        // Assert
        result.ShouldBeSuccess();
        Assert.Equal(2, loggers.Lines.Count);
        Assert.All(loggers.Lines, line => Assert.Equal(WebAppLogs.Category, line.Category));
        Assert.All(loggers.Lines, line => Assert.Equal(1700, line.EventId));
    }

    [Theory]
    [InlineData("uncaught_error", LogLevel.Error)]
    [InlineData("unhandled_rejection", LogLevel.Error)]
    [InlineData("render_failed", LogLevel.Error)]
    [InlineData("service_worker_failed", LogLevel.Error)]
    [InlineData("csp_violation", LogLevel.Warning)]
    public async Task Handle_ShouldDecideTheLevelFromTheEvent(string @event, LogLevel expected)
    {
        // Arrange
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);

        // Act
        await handler.Handle(Command(Record(@event)), Token);

        // Assert
        // The browser never says how serious it is. A page that could choose
        // its own level could page somebody at night.
        Assert.Equal(expected, Assert.Single(loggers.Lines).Level);
    }

    [Fact]
    public async Task Handle_ShouldCarryTheBrowsersStack_AsTheException()
    {
        // Arrange
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);
        const string stack = "at render (https://culina.example/_app/immutable/chunks/a.js:1:42)";

        // Act
        await handler.Handle(Command(Record("uncaught_error") with { Stack = stack }), Token);

        // Assert
        // Where a collector looks for a stack is exception.stacktrace, which
        // is what an attached exception is exported as.
        var exception = Assert.IsType<WebAppException>(Assert.Single(loggers.Lines).Exception);

        Assert.Equal(stack, exception.StackTrace);
        Assert.Contains(stack, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAnEventItDoesNotKnow()
    {
        // Arrange
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);

        // Act
        var result = await handler.Handle(Command(Record("anything_at_all")), Token);

        // Assert
        Assert.Equal("records[0].event", Field(result));
        Assert.Empty(loggers.Lines);
    }

    [Fact]
    public async Task Handle_ShouldRefuseMoreThanTenRecords_AndWriteNoneOfThem()
    {
        // Arrange
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);
        var eleven = Enumerable.Repeat(Record("uncaught_error"), 11).ToArray();

        // Act
        var result = await handler.Handle(Command(eleven), Token);

        // Assert
        Assert.Equal("records", Field(result));
        Assert.Empty(loggers.Lines);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAMessageOverItsCeiling()
    {
        // Arrange
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);
        var record = Record("uncaught_error") with { Message = new string('x', 1_001) };

        // Act
        var result = await handler.Handle(Command(Record("uncaught_error"), record), Token);

        // Assert
        // All or nothing, so a refused batch is never half in the log.
        Assert.Equal("records[1]", Field(result));
        Assert.Empty(loggers.Lines);
    }

    private static CreateLogRecordsCommand Command(params ReportedRecord[] records) =>
        new("2026.10.01-abc", records, "Mozilla/5.0");

    private static ReportedRecord Record(string @event) =>
        new(@event, "Cannot read properties of undefined (reading 'title')", null, "/(app)/recipes/[recipeId]");

    private static string Field(Result result) =>
        result.Match(
            () => throw new InvalidOperationException("Expected a refusal."),
            error => Assert.IsType<FieldError>(error).Field);

    private sealed record Line(string Category, LogLevel Level, int EventId, Exception? Exception);

    /// <summary>Every line written, by every logger it handed out.</summary>
    private sealed class RecordingLoggers : ILoggerFactory
    {
        public List<Line> Lines { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Recording(categoryName, Lines);

        public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException();

        public void Dispose()
        {
        }

        private sealed class Recording(string category, List<Line> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                lines.Add(new Line(category, logLevel, eventId.Id, exception));
        }
    }
}
