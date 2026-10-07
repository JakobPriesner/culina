namespace Api.Infrastructure;

/// <summary>
/// Request-pipeline and security log lines, event ids 1800-1899. Each takes the request, not its path,
/// so the path always goes through <see cref="SecretPaths"/>.
/// </summary>
internal static partial class PipelineLogs
{
    internal static void Unhandled(this ILogger logger, HttpRequest request, Exception exception) =>
        logger.Unhandled(request.Method, SecretPaths.Redact(request.Path), exception);

    internal static void UnreadableBody(this ILogger logger, HttpRequest request)
    {
        // Often below the configured level, and the redaction allocates.
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.UnreadableBody(request.Method, SecretPaths.Redact(request.Path));
        }
    }

    internal static void Rejected(this ILogger logger, HttpRequest request, string reason) =>
        logger.Rejected(request.Method, SecretPaths.Redact(request.Path), reason);

    [LoggerMessage(
        EventId = LogEvents.PipelineBase,
        Level = LogLevel.Error,
        Message = "Unhandled exception while handling {Method} {Path}")]
    private static partial void Unhandled(this ILogger logger, string method, string path, Exception exception);

    [LoggerMessage(
        EventId = LogEvents.PipelineBase + 2,
        Level = LogLevel.Information,
        Message = "Could not read the body of {Method} {Path}")]
    private static partial void UnreadableBody(this ILogger logger, string method, string path);

    [LoggerMessage(
        EventId = LogEvents.PipelineBase + 1,
        Level = LogLevel.Warning,
        Message = "Rejected {Method} {Path}: {Reason}")]
    private static partial void Rejected(this ILogger logger, string method, string path, string reason);
}
