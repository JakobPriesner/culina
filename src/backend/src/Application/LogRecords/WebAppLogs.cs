using Microsoft.Extensions.Logging;

namespace Application.LogRecords;

/// <summary>
/// What the web app reported. Event ids 1700-1799.
/// </summary>
/// <remarks>
/// Its own category, so an operator can tell the browser's lines from the
/// server's with one filter: the exporter sends the category as the
/// instrumentation scope.
/// </remarks>
internal static partial class WebAppLogs
{
    internal const string Category = "Culina.WebApp";

    [LoggerMessage(
        EventId = 1700,
        Message = "The web app {AppVersion} on {UserAgent} reported {Event} at {Route}: {Description}")]
    internal static partial void Reported(
        this ILogger logger,
        LogLevel level,
        string appVersion,
        string userAgent,
        string @event,
        string route,
        string description,
        Exception? exception);
}
