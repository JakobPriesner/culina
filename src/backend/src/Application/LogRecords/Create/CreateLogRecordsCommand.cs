using System.Text.RegularExpressions;
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
/// <param name="Client">
/// The browser and device, as the attributes every record is exported with.
/// </param>
public sealed record CreateLogRecordsCommand(
    string AppVersion,
    IReadOnlyList<ReportedRecord> Records,
    string UserAgent,
    IReadOnlyList<KeyValuePair<string, object>> Client);

/// <summary>One record, as the browser sent it.</summary>
/// <param name="Event">One of <see cref="LogRecordVocabulary.Events"/>.</param>
/// <param name="Message">What it said.</param>
/// <param name="Stack">Where it was thrown, if anywhere.</param>
/// <param name="Route">The route id it happened on.</param>
/// <param name="Context">
/// What the page was like when it happened, as attributes of this record alone.
/// </param>
public sealed record ReportedRecord(
    string Event,
    string Message,
    string? Stack,
    string? Route,
    IReadOnlyList<KeyValuePair<string, object>> Context);

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
/// rate limit per caller and one for every caller together, every line is a
/// <c>Warning</c> however the browser describes it, and no text reaches the log
/// with a line break or another control character in it: a console that does
/// not escape them would otherwise print a forged line of the server's own.
/// </para>
/// </remarks>
internal sealed partial class CreateLogRecordsCommandHandler(ILoggerFactory loggers)
    : ICommandHandler<CreateLogRecordsCommand>
{
    internal const int MostRecords = 10;
    internal const int LongestMessage = 1_000;
    internal const int LongestStack = 8_000;
    internal const int LongestRoute = 200;
    internal const int LongestAppVersion = 64;
    internal const int LongestUserAgent = 256;
    internal const int LongestAttribute = 128;
    internal const int MostListEntries = 10;

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
        if (!logger.IsEnabled(LogLevel.Warning))
        {
            return;
        }

        // Not validated, because nobody chooses it on purpose; cut, because it
        // is still somebody else's text.
        var userAgent = Printable(command.UserAgent.Length > LongestUserAgent
            ? command.UserAgent[..LongestUserAgent]
            : command.UserAgent);
        var appVersion = Printable(command.AppVersion);

        List<KeyValuePair<string, object>> shared =
        [
            new("user_agent.original", userAgent),
            new("culina.web.app_version", appVersion),
            .. command.Client.Select(Bounded)
        ];

        foreach (var record in command.Records)
        {
            var message = Printable(record.Message);

            // A scope rather than more template holes: the exporter turns each
            // pair into an attribute of the line, and the message stays readable.
            using var attributes = logger.BeginScope<IReadOnlyList<KeyValuePair<string, object>>>(
                [.. shared, .. record.Context.Select(Bounded)]);

            logger.Reported(
                appVersion,
                userAgent,
                Printable(record.Event),
                Printable(record.Route ?? "(none)"),
                message,
                record.Stack is { Length: > 0 } stack ? new WebAppException(message, Printable(stack)) : null);
        }
    }

    /// <summary>
    /// Cuts what the browser described itself with rather than refusing it:
    /// losing the error over its context would be the worse trade.
    /// </summary>
    private static KeyValuePair<string, object> Bounded(KeyValuePair<string, object> attribute) =>
        new(attribute.Key, attribute.Value switch
        {
            string text => Cut(text),
            IEnumerable<string> texts => texts.Take(MostListEntries).Select(Cut).ToArray(),
            var value => value
        });

    private static string Cut(string text) =>
        Printable(text.Length > LongestAttribute ? text[..LongestAttribute] : text);

    /// <summary>
    /// The text with every line break and every other control or formatting
    /// character, bidirectional overrides included, turned into a space.
    /// </summary>
    /// <remarks>
    /// A browser's stack loses its line breaks with the rest. Each frame still
    /// starts with its own <c>at</c>, and a stack that can start a new line can
    /// start a forged one.
    /// </remarks>
    private static string Printable(string text) => Unprintable().Replace(text, " ");

    [GeneratedRegex(@"[\p{Cc}\p{Cf}\p{Zl}\p{Zp}]")]
    private static partial Regex Unprintable();

    private static bool Longer(string? value, int limit) => value?.Length > limit;
}
