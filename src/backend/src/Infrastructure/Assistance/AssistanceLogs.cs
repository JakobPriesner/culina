using Microsoft.Extensions.Logging;

namespace Infrastructure.Assistance;

/// <summary>
/// Assistant log lines. Event ids 1500-1509.
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

    [LoggerMessage(
        EventId = 1501,
        Level = LogLevel.Warning,
        Message = "The {Provider} assistant refused a {Capability} request")]
    internal static partial void Refused(ILogger logger, string provider, string capability);

    [LoggerMessage(
        EventId = 1502,
        Level = LogLevel.Error,
        Message = "A {Capability} request to the {Provider} assistant ended in a defect")]
    internal static partial void Failed(
        ILogger logger,
        string capability,
        string provider,
        Exception failure);
}
