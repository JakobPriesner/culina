using System.Globalization;
using Domain.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Api.Infrastructure;

/// <summary>
/// Writes one <see cref="Error"/> as an RFC 9457 problem document.
/// </summary>
/// <remarks>
/// Deferring the write to <see cref="ExecuteAsync"/> is what lets
/// <see cref="CustomResults.Problem"/> take nothing but an error: the
/// correlation id is read from the context at the moment the response is
/// written, so the mapper stays usable as a method group in
/// <c>result.Match(Results.Ok, CustomResults.Problem)</c>.
/// </remarks>
/// <param name="error">The failure to report.</param>
/// <param name="statusOverride">
/// The status to keep instead of the one the error type implies. Used when the
/// framework has already chosen a status — a 401 challenge, a 405 from routing
/// — and the document is being attached to it rather than deciding it.
/// </param>
internal sealed class ProblemResult(Error error, int? statusOverride = null) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var status = statusOverride ?? ErrorStatusCodes.Of(error.Type);

        var problem = new ProblemDetails
        {
            // A URN rather than an http URL: it identifies the problem type
            // unambiguously without promising a web page that a self-hosted
            // instance has no way to serve.
            Type = $"urn:culina:problem:{error.Code}",
            // The framework's table, not a local one: a status added to
            // ErrorStatusCodes.Of would otherwise arrive here as a title of
            // "422" until somebody remembered to edit a second file.
            Title = Phrase(status),
            Status = status,
            Detail = error.Description
        };

        // The code is what clients branch on; the message is prose and will be
        // reworded.
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

        // The content type has to be passed to the write: setting
        // Response.ContentType first does not survive, because the JSON
        // extension sets its own default. Writing directly also keeps this
        // result independent of RequestServices, so middleware can use it
        // before routing has resolved anything.
        await httpContext.Response
            .WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Flattens the contributing failures so a form can mark every wrong field
    /// in one pass. A cause that names no field still appears, with a null
    /// field, rather than being silently dropped.
    /// </summary>
    private static IReadOnlyList<ProblemCause> Describe(ValidationError aggregate) =>
        [.. aggregate.Errors.Select(cause => new ProblemCause(
            (cause as FieldError)?.Field,
            cause.Code,
            cause.Description))];

    /// <summary>
    /// The status's reason phrase, falling back to the number for a status the
    /// framework does not know a phrase for.
    /// </summary>
    private static string Phrase(int status) =>
        ReasonPhrases.GetReasonPhrase(status) is { Length: > 0 } phrase
            ? phrase
            : status.ToString(CultureInfo.InvariantCulture);

    private sealed record ProblemCause(string? Field, string Code, string Detail);
}
