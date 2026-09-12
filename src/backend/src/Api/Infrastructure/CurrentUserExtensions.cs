using System.Globalization;
using Api.Authentication;

namespace Api.Infrastructure;

/// <summary>
/// Reads the authenticated caller from the request.
/// </summary>
/// <remarks>
/// Only for endpoints that declare <c>RequireAuthorization()</c>. Reaching one
/// of these without a principal means the pipeline let an anonymous request
/// through, which is a defect rather than a request outcome.
/// </remarks>
internal static class CurrentUserExtensions
{
    internal static CurrentUser CurrentUser(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new CurrentUser(
            Read(context, CulinaClaims.UserId),
            Read(context, CulinaClaims.SessionId));
    }

    private static Guid Read(HttpContext context, string claim) =>
        context.User.FindFirst(claim)?.Value is { } value
            ? Guid.Parse(value, CultureInfo.InvariantCulture)
            : throw new InvalidOperationException(
                $"The '{claim}' claim is missing. This endpoint must declare RequireAuthorization().");
}

/// <summary>The authenticated caller and the session they are using.</summary>
/// <param name="UserId">Who is calling.</param>
/// <param name="SessionId">Which session they are calling with.</param>
internal readonly record struct CurrentUser(Guid UserId, Guid SessionId);
