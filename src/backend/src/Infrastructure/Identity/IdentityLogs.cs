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
        Message = "Sweeping expired sessions, invitations and finished intake work failed; trying again at the next run")]
    internal static partial void SweepFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1922,
        Level = LogLevel.Information,
        Message = "Removed {Count} invitations that expired unused")]
    internal static partial void SweptInvitations(ILogger logger, int count);

    [LoggerMessage(
        EventId = 1923,
        Level = LogLevel.Information,
        Message = "Removed {Jobs} finished intake jobs and {Notifications} spent push notifications")]
    internal static partial void SweptIntake(ILogger logger, int jobs, int notifications);
}
