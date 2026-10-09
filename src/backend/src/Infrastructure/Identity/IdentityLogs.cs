using Microsoft.Extensions.Logging;

namespace Infrastructure.Identity;

/// <summary>
/// Identity infrastructure log lines. Event ids 1920-1939.
/// </summary>
internal static partial class IdentityLogs
{
    [LoggerMessage(
        EventId = 1920,
        Level = LogLevel.Information,
        Message = "Removed {Count} expired or revoked sessions")]
    internal static partial void SweptSessions(ILogger logger, int count);

    [LoggerMessage(
        EventId = 1921,
        Level = LogLevel.Error,
        Message = "Sweeping expired sessions failed; trying again at the next run")]
    internal static partial void SweepFailed(ILogger logger, Exception exception);
}
