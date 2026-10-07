using System.Globalization;
using Api.Authentication;

namespace Api.Infrastructure;

/// <summary>
/// Reads the authenticated caller; only for endpoints with <c>RequireAuthorization()</c>, as no
/// principal there is a pipeline defect.
/// </summary>
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
