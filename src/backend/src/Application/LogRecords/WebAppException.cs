namespace Application.LogRecords;

/// <summary>
/// A browser error carried as an exception, never thrown, so the exporter writes it as
/// <c>exception.message</c> and <c>exception.stacktrace</c> with the browser's stack.
/// </summary>
/// <param name="message">What the browser said.</param>
/// <param name="stack">Where the browser said it was thrown.</param>
#pragma warning disable CA1032 // Never thrown or constructed by anybody else; the standard constructors would be lies.
public sealed class WebAppException(string message, string stack) : Exception(message)
#pragma warning restore CA1032
{
    /// <inheritdoc />
    public override string StackTrace { get; } = stack;
}
