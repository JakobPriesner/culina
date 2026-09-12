namespace Contracts.Sessions.SignIn;

/// <summary>Who signed in, and the token their requests must echo.</summary>
public sealed record Response
{
    /// <summary>The signed-in user's id.</summary>
    public required Guid UserId { get; init; }

    /// <summary>What they are called.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The address they signed in with.</summary>
    public required string Email { get; init; }

    /// <summary>Whether this account administers the instance.</summary>
    public required bool IsAdmin { get; init; }

    /// <summary>
    /// The CSRF token to send in <c>X-Culina-CSRF</c> on every unsafe request.
    /// </summary>
    /// <remarks>
    /// Returned in the body as well as in a readable cookie, so a client that
    /// signs in can use it immediately without reading cookies at all. It is
    /// not a credential on its own: without the session cookie it authenticates
    /// nothing.
    /// </remarks>
    public required string CsrfToken { get; init; }
}
