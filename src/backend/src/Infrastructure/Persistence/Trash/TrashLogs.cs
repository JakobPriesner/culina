using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Trash;

/// <summary>
/// Trash infrastructure log lines. Event ids 1950-1959.
/// </summary>
internal static partial class TrashLogs
{
    [LoggerMessage(
        EventId = 1950,
        Level = LogLevel.Information,
        Message = "Purged {Count} households, recipes and cookbooks deleted more than the retention ago")]
    internal static partial void Purged(ILogger logger, int count);

    [LoggerMessage(
        EventId = 1951,
        Level = LogLevel.Error,
        Message = "Emptying the bin failed with {Code}")]
    internal static partial void PurgeFailed(ILogger logger, string code);
}
