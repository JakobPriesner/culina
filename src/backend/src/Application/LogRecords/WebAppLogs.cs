using Application.Telemetry;
using Microsoft.Extensions.Logging;

namespace Application.LogRecords;

/// <summary>
/// What the web app reported. Event ids 1700-1799.
/// </summary>
/// <remarks>
/// <para>
/// Its own category, so an operator can tell the browser's lines from the
/// server's on the console with one filter, and so the host can export them
/// under a service of their own.
/// </para>
/// <para>
/// Anybody can send one, signed in or not, so every word of it is a claim
/// rather than a fact: the category and the event name say so, every field is
/// named for the client that supplied it, and the level is never above
/// <c>Warning</c>. An <c>Error</c> is a defect in this server, and a stranger
/// must not be able to write one, or page whoever is on call.
/// </para>
/// </remarks>
internal static partial class WebAppLogs
{
    internal const string Category = CulinaTelemetry.WebAppCategory;

    [LoggerMessage(
        EventId = 1700,
        EventName = "UntrustedWebAppReport",
        Level = LogLevel.Warning,
        Message = "Untrusted report from the web app {ClientAppVersion} on {ClientUserAgent}: "
            + "{ClientEvent} at {ClientRoute}: {ClientMessage}")]
    internal static partial void Reported(
        this ILogger logger,
        string clientAppVersion,
        string clientUserAgent,
        string clientEvent,
        string clientRoute,
        string clientMessage,
        Exception? exception);
}
