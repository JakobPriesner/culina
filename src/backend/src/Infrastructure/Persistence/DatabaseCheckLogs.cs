using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// Database connection check log lines. Event ids 1960-1969.
/// </summary>
internal static partial class DatabaseCheckLogs
{
    /// <summary>
    /// The detail the caller is not told: the socket's or the server's own
    /// words. A warning, because during setup anyone may ask for this check,
    /// and a run of them against different addresses is somebody probing.
    /// </summary>
    [LoggerMessage(
        EventId = 1960,
        Level = LogLevel.Warning,
        Message = "Refused the database at {DatabaseHost}:{DatabasePort} with {Code}: {Reason}")]
    internal static partial void CheckFailed(
        this ILogger logger,
        string databaseHost,
        int databasePort,
        string code,
        string reason);
}
