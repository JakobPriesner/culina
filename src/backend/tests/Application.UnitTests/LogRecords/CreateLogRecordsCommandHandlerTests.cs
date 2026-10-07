using Application.LogRecords;
using Application.LogRecords.Create;
using Domain.Shared;
using Microsoft.Extensions.Logging;
using TestSupport;

namespace Application.UnitTests.LogRecords;

/// <summary>What the browser's reports turn into on the server, and what is refused.</summary>
/// <remarks>
/// Anybody can send these, so what matters is that a caller cannot choose how loud a line is or how
/// much of it there is.
/// </remarks>
public class CreateLogRecordsCommandHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ShouldWriteOneLinePerRecord_InTheWebAppCategory()
    {
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);

        var result = await handler.Handle(
            Command(Record("uncaught_error"), Record("unhandled_rejection")),
            Token);

        result.ShouldBeSuccess();
        Assert.Equal(2, loggers.Lines.Count);
        Assert.All(loggers.Lines, line => Assert.Equal("Culina.WebApp.Untrusted", line.Category));
        Assert.All(loggers.Lines, line => Assert.Equal(1700, line.EventId));
        Assert.All(loggers.Lines, line => Assert.Equal("UntrustedWebAppReport", line.EventName));
    }

    [Theory]
    [InlineData("uncaught_error")]
    [InlineData("unhandled_rejection")]
    [InlineData("render_failed")]
    [InlineData("service_worker_failed")]
    [InlineData("csp_violation")]
    public async Task Handle_ShouldWriteAWarning_WhateverTheEvent(string @event)
    {
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);

        await handler.Handle(Command(Record(@event)), Token);

        // Anybody can send one: an Error is a defect in this server, and a stranger writing one
        // could page somebody at night.
        Assert.Equal(LogLevel.Warning, Assert.Single(loggers.Lines).Level);
    }

    [Fact]
    public async Task Handle_ShouldNameEveryFieldAsTheClients_SoNoneReadsAsTheServersOwn()
    {
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);

        await handler.Handle(Command(Record("uncaught_error")), Token);

        var line = Assert.Single(loggers.Lines);
        Assert.StartsWith("Untrusted report from the web app", line.Message, StringComparison.Ordinal);
        Assert.Equal(
            ["ClientAppVersion", "ClientEvent", "ClientMessage", "ClientRoute", "ClientUserAgent"],
            line.Fields.Keys.Where(key => key != "{OriginalFormat}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Handle_ShouldReplaceLineBreaksAndControlCharacters_InEverythingTheCallerSent()
    {
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);
        const string forged = "\r\n12:00:00 fail: Api[1800] Unhandled exception\u0007\u202e\u2028";
        var record = Record("uncaught_error") with
        {
            Message = "broke" + forged,
            Stack = "at a" + forged,
            Route = "/route" + forged,
            Context = [new("culina.web.occurred_at", "now" + forged)]
        };
        var command = Command(record) with
        {
            AppVersion = "v1" + forged,
            UserAgent = "Mozilla" + forged,
            Client = [new("browser.platform", "macOS" + forged), new("culina.web.languages", new[] { "de" + forged })]
        };

        await handler.Handle(command, Token);

        // A console that does not escape a line break would print the rest as
        // a line of the server's own.
        var line = Assert.Single(loggers.Lines);
        string[] written =
        [
            line.Message,
            line.Exception!.Message,
            line.Exception.StackTrace!,
            .. line.Fields.Values.Select(value => $"{value}"),
            .. line.Attributes.Values.Select(value => value is string[] texts ? string.Join(",", texts) : $"{value}")
        ];

        Assert.All(written, text => Assert.DoesNotContain(text, character =>
            char.IsControl(character) || character is '\u202e' or '\u2028'));
        Assert.Contains("broke  12:00:00 fail: Api[1800] Unhandled exception", line.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_ShouldCarryTheBrowsersStack_AsTheException()
    {
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);
        const string stack = "at render (https://culina.example/_app/immutable/chunks/a.js:1:42)";

        await handler.Handle(Command(Record("uncaught_error") with { Stack = stack }), Token);

        // Where a collector looks for a stack is exception.stacktrace, which
        // is what an attached exception is exported as.
        var exception = Assert.IsType<WebAppException>(Assert.Single(loggers.Lines).Exception);

        Assert.Equal(stack, exception.StackTrace);
        Assert.Contains(stack, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAnEventItDoesNotKnow()
    {
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);

        var result = await handler.Handle(Command(Record("anything_at_all")), Token);

        Assert.Equal("records[0].event", Field(result));
        Assert.Empty(loggers.Lines);
    }

    [Fact]
    public async Task Handle_ShouldRefuseMoreThanTenRecords_AndWriteNoneOfThem()
    {
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);
        var eleven = Enumerable.Repeat(Record("uncaught_error"), 11).ToArray();

        var result = await handler.Handle(Command(eleven), Token);

        Assert.Equal("records", Field(result));
        Assert.Empty(loggers.Lines);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAMessageOverItsCeiling()
    {
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);
        var record = Record("uncaught_error") with { Message = new string('x', 1_001) };

        var result = await handler.Handle(Command(Record("uncaught_error"), record), Token);

        // All or nothing, so a refused batch is never half in the log.
        Assert.Equal("records[1]", Field(result));
        Assert.Empty(loggers.Lines);
    }

    private static CreateLogRecordsCommand Command(params ReportedRecord[] records) =>
        new("2026.10.01-abc", records, "Mozilla/5.0", []);

    private static ReportedRecord Record(string @event) =>
        new(@event, "Cannot read properties of undefined (reading 'title')", null, "/(app)/recipes/[recipeId]", []);

    [Fact]
    public async Task Handle_ShouldAttachTheBrowsersDetails_CutToTheirCeiling()
    {
        var loggers = new RecordingLoggers();
        var handler = new CreateLogRecordsCommandHandler(loggers);
        var command = Command(Record("uncaught_error") with { Context = [new("culina.web.online", false)] }) with
        {
            Client =
            [
                new("browser.platform", new string('x', 500)),
                new("culina.web.languages", Enumerable.Repeat("de", 50).ToArray()),
                new("culina.web.screen.width", 390)
            ]
        };

        await handler.Handle(command, Token);

        // Each one an attribute of the line, and none of them an open door:
        // anybody can send these.
        var attributes = Assert.Single(loggers.Lines).Attributes;

        Assert.Equal("Mozilla/5.0", attributes["user_agent.original"]);
        Assert.Equal("2026.10.01-abc", attributes["culina.web.app_version"]);
        Assert.Equal(128, Assert.IsType<string>(attributes["browser.platform"]).Length);
        Assert.Equal(10, Assert.IsType<string[]>(attributes["culina.web.languages"]).Length);
        Assert.Equal(390, attributes["culina.web.screen.width"]);
        Assert.Equal(false, attributes["culina.web.online"]);
    }

    private static string Field(Result result) =>
        result.Match(
            () => throw new InvalidOperationException("Expected a refusal."),
            error => Assert.IsType<FieldError>(error).Field);

    private sealed record Line(
        string Category,
        LogLevel Level,
        int EventId,
        string? EventName,
        string Message,
        IReadOnlyDictionary<string, object?> Fields,
        Exception? Exception,
        IReadOnlyDictionary<string, object> Attributes);

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
            private IReadOnlyDictionary<string, object> scope = new Dictionary<string, object>();

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                if (state is IEnumerable<KeyValuePair<string, object>> pairs)
                {
                    scope = pairs.ToDictionary();
                }

                return null;
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                lines.Add(new Line(
                    category,
                    logLevel,
                    eventId.Id,
                    eventId.Name,
                    formatter(state, exception),
                    (state as IEnumerable<KeyValuePair<string, object?>>)?.ToDictionary() ?? new Dictionary<string, object?>(),
                    exception,
                    scope));
        }
    }
}
