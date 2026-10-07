using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>Database connection check log lines. Event ids 1960-1969.</summary>
internal static partial class DatabaseCheckLogs
{
    /// <summary>
    /// The detail the caller is not told. A warning: anyone may ask for this check during setup,
    /// and a run of them is probing.
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
