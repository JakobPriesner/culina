using Domain.Shared;

namespace Api.Infrastructure;

/// <summary>
/// Maps an <see cref="ErrorType"/> to its HTTP status code.
/// </summary>
/// <remarks>
/// The only place in the application where this translation happens, so a new
/// error type cannot reach a client as an accidental 500.
/// </remarks>
internal static class ErrorStatusCodes
{
    internal static int Of(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
        ErrorType.RateLimited => StatusCodes.Status429TooManyRequests,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorType.Failure or ErrorType.Problem => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError
    };
}
