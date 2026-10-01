using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.LogRecords;
using Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Application.LogRecords.Create;

/// <summary>Writes what the web app reported into the server's own log.</summary>
/// <param name="AppVersion">The build that reported it.</param>
/// <param name="Records">What it reported.</param>
/// <param name="UserAgent">The browser, as its request header names it.</param>
public sealed record CreateLogRecordsCommand(
    string AppVersion,
    IReadOnlyList<ReportedRecord> Records,
    string UserAgent);

/// <summary>One record, as the browser sent it.</summary>
/// <param name="Event">One of <see cref="LogRecordVocabulary.Events"/>.</param>
/// <param name="Message">What it said.</param>
/// <param name="Stack">Where it was thrown, if anywhere.</param>
/// <param name="Route">The route id it happened on.</param>
public sealed record ReportedRecord(string Event, string Message, string? Stack, string? Route);

/// <summary>
/// Re-emits browser records through <see cref="ILogger"/>.
/// </summary>
/// <remarks>
/// <para>
/// Through the server rather than from the browser to a collector: the
/// collector stays on the operator's network, the page's
/// <c>connect-src 'self'</c> stays as it is, and the records leave with the
/// exporter the server is already configured with — the request id, the user
/// id and the trace come along from the logging scope for free.
/// </para>
/// <para>
/// Anybody can call this, signed in or not, because a sign-in page can break
/// too. So every field has a ceiling, the batch has one, the endpoint has a
/// rate limit of its own, and the level is decided here from the event code
/// rather than taken from the request.
/// </para>
/// </remarks>
internal sealed class CreateLogRecordsCommandHandler(ILoggerFactory loggers)
    : ICommandHandler<CreateLogRecordsCommand>
{
    internal const int MostRecords = 10;
    internal const int LongestMessage = 1_000;
    internal const int LongestStack = 8_000;
    internal const int LongestRoute = 200;
    internal const int LongestAppVersion = 64;
    internal const int LongestUserAgent = 256;

    private const string Invalid = "log_records.invalid";

    private readonly ILogger logger = loggers.CreateLogger(WebAppLogs.Category);

    public Task<Result> Handle(CreateLogRecordsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("LogRecords.Create");

        var valid = Validate(command);

        valid.Match(() => Write(command), _ => { });

        return Task.FromResult(tracked.Record(valid));
    }

    private static Result Validate(CreateLogRecordsCommand command)
    {
        if (command.Records.Count is 0 or > MostRecords)
        {
            return new FieldError("records", Invalid, $"Send between 1 and {MostRecords} records.");
        }

        if (command.AppVersion.Length is 0 or > LongestAppVersion)
        {
            return new FieldError(
                "appVersion",
                Invalid,
                $"The app version must be 1 to {LongestAppVersion} characters.");
        }

        for (var index = 0; index < command.Records.Count; index++)
        {
            var record = command.Records[index];
            var field = $"records[{index}]";

            if (!LogRecordVocabulary.Events.Contains(record.Event))
            {
                return new FieldError($"{field}.event", Invalid, $"'{record.Event}' is not an event this records.");
            }

            if (Longer(record.Message, LongestMessage)
                || Longer(record.Stack, LongestStack)
                || Longer(record.Route, LongestRoute))
            {
                return new FieldError(
                    field,
                    Invalid,
                    $"A message is at most {LongestMessage} characters, a stack {LongestStack} and a route {LongestRoute}.");
            }
        }

        return Result.Success();
    }

    private void Write(CreateLogRecordsCommand command)
    {
        // Not validated, because nobody chooses it on purpose; cut, because it
        // is still somebody else's text.
        var userAgent = command.UserAgent.Length > LongestUserAgent
            ? command.UserAgent[..LongestUserAgent]
            : command.UserAgent;

        foreach (var record in command.Records)
        {
            var level = LevelOf(record.Event);

            if (!logger.IsEnabled(level))
            {
                continue;
            }

            logger.Reported(
                level,
                command.AppVersion,
                userAgent,
                record.Event,
                record.Route ?? "(none)",
                record.Message,
                record.Stack is { Length: > 0 } stack ? new WebAppException(record.Message, stack) : null);
        }
    }

    /// <summary>
    /// A blocked resource is the policy working, which is security-relevant
    /// rather than a defect; everything else is the app being wrong.
    /// </summary>
    private static LogLevel LevelOf(string @event) =>
        @event == LogRecordVocabulary.CspViolation ? LogLevel.Warning : LogLevel.Error;

    private static bool Longer(string? value, int limit) => value?.Length > limit;
}
