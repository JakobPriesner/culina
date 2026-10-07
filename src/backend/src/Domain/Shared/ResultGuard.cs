namespace Domain.Shared;

/// <summary>Rejects observation of a <c>default</c> result: it is neither success nor failure, so it is a defect and throws.</summary>
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
