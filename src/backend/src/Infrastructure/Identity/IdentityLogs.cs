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
}
