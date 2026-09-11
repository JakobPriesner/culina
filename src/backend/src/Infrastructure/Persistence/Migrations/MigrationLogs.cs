using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Migrations;

/// <summary>
/// Migration log lines. Event ids 1900-1919 belong to schema migration.
/// </summary>
/// <remarks>
/// Source-generated so the template is checked at compile time, the disabled
/// path allocates nothing, and no message can be accidentally interpolated.
/// </remarks>
internal static partial class MigrationLogs
{
    [LoggerMessage(
        EventId = 1900,
        Level = LogLevel.Information,
        Message = "Applied migration {Version} in {ElapsedMilliseconds} ms")]
    internal static partial void Applied(this ILogger logger, string version, long elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1901,
        Level = LogLevel.Information,
        Message = "Schema is up to date at migration {Version}")]
    internal static partial void UpToDate(this ILogger logger, string version);

    [LoggerMessage(
        EventId = 1902,
        Level = LogLevel.Critical,
        Message = "Migration {Version} has already been applied but its contents have changed. "
            + "Migrations are forward-only: restore the file and add a new migration instead.")]
    internal static partial void ChecksumMismatch(this ILogger logger, string version);

    [LoggerMessage(
        EventId = 1903,
        Level = LogLevel.Critical,
        Message = "Migration {Version} failed. The database has not been changed by it and the "
            + "process will not serve traffic.")]
    internal static partial void Failed(this ILogger logger, string version, Exception exception);
}
