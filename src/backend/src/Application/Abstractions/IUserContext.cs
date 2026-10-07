namespace Application.Abstractions;

/// <summary>The identity of the caller handling the current request.</summary>
/// <remarks>
/// Application code never sees <c>HttpContext</c>: this port is all a handler may know about the
/// caller. Identity only, with no I/O; whether a user may act on a household is for the household
/// repository and <c>HouseholdMembershipPolicy</c>.
/// </remarks>
public interface IUserContext
{
    /// <summary>Whether the request carries a valid session.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The authenticated user's id.</summary>
    /// <exception cref="InvalidOperationException">The request is not authenticated: every endpoint
    /// reaching such a handler requires authorization, so this is a pipeline defect, thrown not
    /// returned.</exception>
    Guid UserId { get; }
}
