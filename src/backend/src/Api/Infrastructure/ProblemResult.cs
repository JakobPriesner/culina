using System.Globalization;
using Domain.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Api.Infrastructure;

/// <summary>Writes one <see cref="Error"/> as an RFC 9457 problem document.</summary>
/// <remarks>
/// The write is deferred to <see cref="ExecuteAsync"/> so the correlation id is read at write time
/// and <see cref="CustomResults.Problem"/> can be a method group over an error.
/// </remarks>
/// <param name="error">The failure to report.</param>
/// <param name="statusOverride">
/// A status the framework already chose (a 401 challenge, a routed 405) to keep.
/// </param>
internal sealed class ProblemResult(Error error, int? statusOverride = null) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var status = statusOverride ?? ErrorStatusCodes.Of(error.Type);

        var problem = new ProblemDetails
        {
            // A URN, not an http URL: it promises no web page a self-hosted instance could not
            // serve.
            Type = $"urn:culina:problem:{error.Code}",
            // The framework's table, so a status added to ErrorStatusCodes.Of never shows up as the
            // title "422".
            Title = Phrase(status),
            Status = status,
            Detail = error.Description
        };

        // The code is what clients branch on; the message is prose and will be reworded.
        problem.Extensions["code"] = error.Code;

        if (RequestContext.RequestId(httpContext) is { } requestId)
        {
            problem.Extensions["requestId"] = requestId;
        }

        if (error is ValidationError aggregate)
        {
            problem.Extensions["errors"] = Describe(aggregate);
        }

        httpContext.Response.StatusCode = status;
        RequestContext.SetErrorCode(httpContext, error.Code);

        // The content type must be passed to the write (the JSON extension overrides
        // Response.ContentType); writing directly also keeps this independent of RequestServices,
        // for middleware before routing.
        await httpContext.Response
            .WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Flattens the contributing failures so a form can mark every wrong field; a cause with no
    /// field is kept with a null field.
    /// </summary>
    private static IReadOnlyList<ProblemCause> Describe(ValidationError aggregate) =>
        [.. aggregate.Errors.Select(cause => new ProblemCause(
            (cause as FieldError)?.Field,
            cause.Code,
            cause.Description))];

    /// <summary>The status's reason phrase, or the number when the framework has none.</summary>
    private static string Phrase(int status) =>
        ReasonPhrases.GetReasonPhrase(status) is { Length: > 0 } phrase
            ? phrase
            : status.ToString(CultureInfo.InvariantCulture);

    private sealed record ProblemCause(string? Field, string Code, string Detail);
}
