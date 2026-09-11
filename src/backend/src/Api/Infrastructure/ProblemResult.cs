using System.Globalization;
using Domain.Shared;
using Microsoft.AspNetCore.Mvc;

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
internal sealed class ProblemResult(Error error) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var status = ErrorStatusCodes.Of(error.Type);

        var problem = new ProblemDetails
        {
            // A URN rather than an http URL: it identifies the problem type
            // unambiguously without promising a web page that a self-hosted
            // instance has no way to serve.
            Type = $"urn:culina:problem:{error.Code}",
            Title = ReasonPhrases.GetReasonPhrase(status),
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

    private sealed record ProblemCause(string? Field, string Code, string Detail);
}

/// <summary>The reason phrases used as problem titles.</summary>
internal static class ReasonPhrases
{
    internal static string GetReasonPhrase(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status405MethodNotAllowed => "Method Not Allowed",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status412PreconditionFailed => "Precondition Failed",
        StatusCodes.Status428PreconditionRequired => "Precondition Required",
        StatusCodes.Status429TooManyRequests => "Too Many Requests",
        StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
        StatusCodes.Status500InternalServerError => "Internal Server Error",
        _ => statusCode.ToString(CultureInfo.InvariantCulture)
    };
}
