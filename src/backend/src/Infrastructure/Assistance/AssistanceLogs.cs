using Microsoft.Extensions.Logging;

namespace Infrastructure.Assistance;

/// <summary>Assistant log lines. Event ids 1500-1509; listing models uses 1510-1519.</summary>
/// <remarks>
/// Nothing here carries a prompt, answer, recipe or key: the content is the person's. What is worth
/// recording is that a provider refused, and why.
/// </remarks>
internal static partial class AssistanceLogs
{
    [LoggerMessage(
        EventId = 1500,
        Level = LogLevel.Warning,
        Message = "The {Provider} assistant answered with nothing usable (finish reason: {FinishReason})")]
    internal static partial void EmptyAnswer(ILogger logger, string provider, string? finishReason);

    /// <remarks>
    /// The exception goes with it: the provider's status and words tell a revoked key from a
    /// content filter from a renamed model, which the person's error code cannot.
    /// </remarks>
    [LoggerMessage(
        EventId = 1501,
        Level = LogLevel.Warning,
        Message = "A call to the {Provider} assistant failed and was reported as {Code}")]
    internal static partial void CallFailed(ILogger logger, string provider, string code, Exception failure);
}
