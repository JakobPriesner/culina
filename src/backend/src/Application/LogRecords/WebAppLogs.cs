using Application.Telemetry;
using Microsoft.Extensions.Logging;

namespace Application.LogRecords;

/// <summary>What the web app reported. Event ids 1700-1799.</summary>
/// <remarks>
/// Its own category, so the browser's lines filter apart and export as their own service. Anybody
/// can send one, so every field is a client claim and the level never exceeds <c>Warning</c>: a
/// stranger must not write an <c>Error</c> or page on-call.
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
