namespace Api.Infrastructure;

/// <summary>
/// Request-pipeline and security log lines. Event ids 1800-1899.
/// </summary>
/// <remarks>
/// Source-generated: the template is checked at compile time, the disabled path
/// allocates nothing, and no message can be accidentally interpolated.
/// </remarks>
internal static partial class PipelineLogs
{
    [LoggerMessage(
        EventId = LogEvents.PipelineBase,
        Level = LogLevel.Error,
        Message = "Unhandled exception while handling {Method} {Path}")]
    internal static partial void Unhandled(this ILogger logger, string method, string path, Exception exception);

    [LoggerMessage(
        EventId = LogEvents.PipelineBase + 2,
        Level = LogLevel.Information,
        Message = "Could not read the body of {Method} {Path}")]
    internal static partial void UnreadableBody(this ILogger logger, string method, string path);

    [LoggerMessage(
        EventId = LogEvents.PipelineBase + 1,
        Level = LogLevel.Warning,
        Message = "Rejected {Method} {Path}: {Reason}")]
    internal static partial void Rejected(this ILogger logger, string method, string path, string reason);
}
