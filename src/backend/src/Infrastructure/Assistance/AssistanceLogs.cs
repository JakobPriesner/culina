using Microsoft.Extensions.Logging;

namespace Infrastructure.Assistance;

/// <summary>
/// Assistant log lines. Event ids 1500-1509; listing models uses 1510-1519.
/// </summary>
/// <remarks>
/// Nothing here carries a prompt, an answer, a recipe or a key. What is worth
/// recording is that a provider refused and what it said about why — the
/// content is the person's, and an operator reading the logs has no business
/// with it.
/// </remarks>
internal static partial class AssistanceLogs
{
    [LoggerMessage(
        EventId = 1500,
        Level = LogLevel.Warning,
        Message = "The {Provider} assistant answered with nothing usable (finish reason: {FinishReason})")]
    internal static partial void EmptyAnswer(ILogger logger, string provider, string? finishReason);

    /// <remarks>
    /// The exception goes with it: the provider's status and its own words are
    /// what tell a revoked key from a content filter from a model that was
    /// renamed, and the error code the person sees cannot.
    /// </remarks>
    [LoggerMessage(
        EventId = 1501,
        Level = LogLevel.Warning,
        Message = "A call to the {Provider} assistant failed and was reported as {Code}")]
    internal static partial void CallFailed(ILogger logger, string provider, string code, Exception failure);
}
