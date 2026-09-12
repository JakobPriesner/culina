using System.Globalization;
using System.Security.Claims;
using Api.Authentication;
using Application.Abstractions;

namespace Api.Infrastructure;

/// <summary>
/// The <see cref="IUserContext"/> port, over the current HTTP request.
/// </summary>
/// <remarks>
/// This wrapper is the reason no handler ever sees <c>HttpContext</c>: the port
/// says exactly what Application may know about the caller, and a unit test
/// satisfies it with a constructor argument.
/// </remarks>
/// <param name="accessor">The current request.</param>
internal sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    public bool IsAuthenticated => Claim(CulinaClaims.UserId) is not null;

    public Guid UserId => Claim(CulinaClaims.UserId) is { } value
        ? Guid.Parse(value, CultureInfo.InvariantCulture)
        : throw new InvalidOperationException(
            "This request is not authenticated. An endpoint whose handler needs a user id "
            + "must declare RequireAuthorization(), so reaching here is a pipeline defect.");

    /// <summary>The session the request is using, for revoking this device.</summary>
    internal Guid? SessionId => Claim(CulinaClaims.SessionId) is { } value
        ? Guid.Parse(value, CultureInfo.InvariantCulture)
        : null;

    private string? Claim(string type) =>
        (accessor.HttpContext?.User.Identity as ClaimsIdentity)?.FindFirst(type)?.Value;
}
