namespace Application.LogRecords;

/// <summary>
/// An error that happened in the browser, carried as an exception so it is
/// exported as one.
/// </summary>
/// <remarks>
/// Never thrown. It exists because the exporter writes an exception attached
/// to a log line as <c>exception.message</c> and <c>exception.stacktrace</c>,
/// which is where a collector looks for a stack — and the stack is the
/// browser's, so it replaces the one this object would otherwise have.
/// </remarks>
/// <param name="message">What the browser said.</param>
/// <param name="stack">Where the browser said it was thrown.</param>
#pragma warning disable CA1032 // Never thrown or constructed by anybody else; the standard constructors would be lies.
public sealed class WebAppException(string message, string stack) : Exception(message)
#pragma warning restore CA1032
{
    /// <inheritdoc />
    public override string StackTrace { get; } = stack;
}
