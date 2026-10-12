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
    public required string CsrfToken { get; init; }

    /// <summary>
    /// The session token, for an app only (<c>client</c> <c>ios</c> or <c>android</c>); null for a
    /// browser, whose token is the HttpOnly cookie and never readable by script. Sent as
    /// <c>Authorization: Bearer</c>, it needs no CSRF token.
    /// </summary>
    public string? SessionToken { get; init; }
}
