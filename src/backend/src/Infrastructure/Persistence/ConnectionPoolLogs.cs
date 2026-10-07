using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>Connection pool log lines. Event ids 1970-1979.</summary>
internal static partial class ConnectionPoolLogs
{
    /// <summary>A request waited for a connection; an early warning before pool exhaustion.</summary>
    [LoggerMessage(
        EventId = 1970,
        Level = LogLevel.Warning,
        Message = "Waited {WaitMilliseconds} ms for a database connection; {Unreported} other slow waits "
            + "went unreported since the last one. Database__MaxPoolSize may be too small for the load, "
            + "or something is keeping connections out of the pool")]
    internal static partial void SlowConnection(this ILogger logger, long waitMilliseconds, int unreported);

    /// <summary>A connection was held out of the pool longer than any statement or transaction should take.</summary>
    [LoggerMessage(
        EventId = 1971,
        Level = LogLevel.Warning,
        Message = "Held a database connection for {HeldMilliseconds} ms in {UseCase}; {Unreported} other "
            + "long holds went unreported since the last one")]
    internal static partial void ConnectionHeldLong(
        this ILogger logger,
        long heldMilliseconds,
        string useCase,
        int unreported);
}
