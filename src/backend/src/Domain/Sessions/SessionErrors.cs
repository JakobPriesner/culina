using Domain.Shared;

namespace Domain.Sessions;

/// <summary>Failures relating to signing in and staying signed in.</summary>
public static class SessionErrors
{
    /// <summary>
    /// The credentials did not match.
    /// </summary>
    /// <remarks>
    /// One error for "no such account" and "wrong password", always. Telling
    /// them apart turns the sign-in form into a way to discover which addresses
    /// are registered.
    /// </remarks>
    public static readonly Error InvalidCredentials = new(
        "auth.invalid_credentials",
        "That email address and password do not match.",
        ErrorType.Unauthorized);

    /// <summary>The request carries no valid session.</summary>
    public static readonly Error NotAuthenticated = new(
        "auth.not_authenticated",
        "You need to sign in to do that.",
        ErrorType.Unauthorized);

    /// <summary>The session id in the URL does not belong to the caller.</summary>
    public static readonly Error SessionNotFound = new(
        "auth.session_not_found",
        "That session does not exist.",
        ErrorType.NotFound);

    /// <summary>The CSRF token was missing or did not match.</summary>
    public static readonly Error CsrfInvalid = new(
        "auth.csrf_invalid",
        "This request could not be verified. Reload the page and try again.",
        ErrorType.Forbidden);
}
