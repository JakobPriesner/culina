namespace Application.Abstractions;

/// <summary>
/// The identity of the caller handling the current request.
/// </summary>
/// <remarks>
/// <para>
/// Application code never sees <c>HttpContext</c>. This port is the whole of
/// what a handler may know about who is calling; Infrastructure implements it
/// over the ASP.NET Core request, and a unit test implements it with a
/// constructor argument.
/// </para>
/// <para>
/// It carries identity only, and deliberately performs no I/O. Whether a user
/// may act on a household is a question for the household repository and
/// <c>HouseholdMembershipPolicy</c>, not for an ambient context object.
/// </para>
/// </remarks>
public interface IUserContext
{
    /// <summary>Whether the request carries a valid session.</summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// The authenticated user's id.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The request is not authenticated. Every endpoint that reaches a handler
    /// needing a user id declares <c>RequireAuthorization()</c>, so reaching
    /// here unauthenticated is a pipeline defect rather than a request outcome
    /// — and is therefore thrown, not returned.
    /// </exception>
    Guid UserId { get; }
}
