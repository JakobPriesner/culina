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

    /// <summary>
    /// The address and recovery code do not unlock an account.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One error for an unknown address, a wrong code, a used code, an expired
    /// code and another account's code. Any distinction between them tells
    /// whoever is guessing which guesses were close.
    /// </para>
    /// <para>
    /// A validation failure rather than a 401, because nobody is signed in to
    /// be told they are not: a 401 is what every client reads as "your session
    /// ended", and answering a typo with that would throw the person off the
    /// form they are filling in.
    /// </para>
    /// </remarks>
    public static readonly Error InvalidRecoveryCode = new(
        "auth.invalid_recovery_code",
        "That email address and recovery code do not match.",
        ErrorType.Validation);
}
