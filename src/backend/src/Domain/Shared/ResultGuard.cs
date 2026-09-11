namespace Domain.Shared;

/// <summary>
/// Rejects observation of a <c>default</c> result.
/// </summary>
/// <remarks>
/// A default-constructed result is neither a success nor a failure, so it has no
/// meaning. Treating it as either would hide the bug that produced it, and
/// inventing a third state would force every caller to handle it. It is a
/// defect, so it throws.
/// </remarks>
internal static class ResultGuard
{
    internal static void AgainstUninitialised(bool observable)
    {
        if (!observable)
        {
            throw new InvalidOperationException(
                "This result was never assigned an outcome. A default(Result) is a bug: "
                + "build one with Result.Success(), Result.Failure(error), or by returning an Error.");
        }
    }
}
