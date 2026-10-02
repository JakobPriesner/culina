namespace Api.Infrastructure;

/// <summary>
/// Setting the instance up, and restarting to apply server settings. Event ids
/// 1600-1699.
/// </summary>
internal static partial class ServerLogs
{
    /// <summary>
    /// A warning rather than information: an operator who started the
    /// container and sees nothing in the browser should find the reason as the
    /// loudest line in the log.
    /// </summary>
    [LoggerMessage(
        EventId = LogEvents.ServerBase,
        Level = LogLevel.Warning,
        Message = "No database is configured. Culina is serving its setup screen only: open it in a browser to connect one, or set the Database__* variables")]
    internal static partial void WaitingForSetup(this ILogger logger);

    [LoggerMessage(
        EventId = LogEvents.ServerBase + 1,
        Level = LogLevel.Information,
        Message = "Restarting to apply saved server settings")]
    internal static partial void Restarting(this ILogger logger);

    /// <summary>
    /// What this instance is and where it keeps things, first in the log: the
    /// questions behind most reports that something is wrong. No credentials,
    /// and only the collector's host, since an address can carry a token.
    /// </summary>
    [LoggerMessage(
        EventId = LogEvents.ServerBase + 2,
        Level = LogLevel.Information,
        Message = "Culina {Version} starting in {Environment}: database {DatabaseHost}:{DatabasePort}/{DatabaseName}, images in {ImagePath}, telemetry export to {TelemetryExport}")]
    internal static partial void Starting(
        this ILogger logger,
        string version,
        string environment,
        string databaseHost,
        int databasePort,
        string databaseName,
        string imagePath,
        string telemetryExport);
}
