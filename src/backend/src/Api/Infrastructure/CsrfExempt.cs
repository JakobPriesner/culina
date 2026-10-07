namespace Api.Infrastructure;

/// <summary>
/// Marks an endpoint that is not subject to the session CSRF check.
/// </summary>
/// <remarks>
/// <para>
/// Only the ways back in carry this, and each needs a reason. The first is
/// <c>POST /sessions</c>. The CSRF token is the thing that proves a request
/// came from our own page, and it lives in a readable cookie beside the
/// <c>HttpOnly</c> session cookie. If the readable one is lost while the
/// session cookie survives — a browser that clears non-HttpOnly cookies, an
/// extension, a person clearing what their developer tools let them clear —
/// then every unsafe request fails, including sign-out. The app is wedged, and
/// "reload the page and try again" does not help, because reloading does not
/// bring the token back.
/// </para>
/// <para>
/// Signing in again is the way out, so signing in cannot be the thing that is
/// blocked. Setting a new password with a recovery code is the same kind of
/// way back, for somebody with no working session at all.
/// </para>
/// <para>
/// What keeps a foreign page from signing a browser in to an account of its
/// choosing is not the same-origin guard — that only looks at requests already
/// carrying a session cookie, and the browser a login CSRF targets usually has
/// none. It is that these endpoints accept only an <c>application/json</c>
/// body, and a cross-site form can send nothing but form-encoded, multipart or
/// <c>text/plain</c> bodies; anything else from another origin needs a CORS
/// preflight, which Culina never grants. A test pins it for sign-in
/// (<c>SignIn_ShouldRefuseEveryBodyACrossSiteFormCanSend</c>), so an endpoint
/// that one day also accepted a form would fail it.
/// </para>
/// </remarks>
internal sealed class CsrfExempt;
