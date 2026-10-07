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
}
