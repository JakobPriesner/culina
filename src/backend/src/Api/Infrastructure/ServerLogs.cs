namespace Api.Infrastructure;

/// <summary>
/// Setting the instance up, and restarting to apply server settings. Event ids 1600-1699.
/// </summary>
internal static partial class ServerLogs
{
    /// <summary>
    /// A warning, not information: an operator who sees nothing in the browser should find the
    /// reason as the loudest log line.
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
    /// One address asked for more database checks than setup needs; the address is on the request's
    /// scope.
    /// </summary>
    [LoggerMessage(
        EventId = LogEvents.ServerBase + 4,
        Level = LogLevel.Warning,
        Message = "Refused a database check: more than {AttemptsPerMinute} a minute from one address")]
    internal static partial void DatabaseChecksLimited(this ILogger logger, int attemptsPerMinute);

    /// <summary>
    /// What this instance is and where it keeps things, first in the log. No credentials, and only
    /// the collector's host (an address can carry a token).
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

    /// <summary>
    /// Every start without secure cookies, Development included, so such an instance says so in its
    /// log.
    /// </summary>
    [LoggerMessage(
        EventId = LogEvents.ServerBase + 3,
        Level = LogLevel.Warning,
        Message = "Cookies are not Secure in {Environment}: the session cookie goes without its __Host- prefix and travels in cleartext over plain HTTP. Only local development should run like this")]
    internal static partial void InsecureCookies(this ILogger logger, string environment);
}
