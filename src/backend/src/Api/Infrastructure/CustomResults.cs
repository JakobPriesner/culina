using Domain.Shared;

namespace Api.Infrastructure;

/// <summary>
/// The single place an <see cref="Error"/> becomes an HTTP response, so every rejection has one
/// format.
/// </summary>
internal static class CustomResults
{
    /// <summary>Renders a failure as an RFC 9457 problem document.</summary>
    internal static IResult Problem(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new ProblemResult(error);
    }

    /// <summary>
    /// Writes a problem document directly, for middleware rejecting a request before routing;
    /// <paramref name="statusOverride"/> keeps a status the framework chose.
    /// </summary>
    internal static Task WriteProblemAsync(HttpContext context, Error error, int? statusOverride = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(error);

        return new ProblemResult(error, statusOverride).ExecuteAsync(context);
    }
}
