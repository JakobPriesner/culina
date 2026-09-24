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
}
