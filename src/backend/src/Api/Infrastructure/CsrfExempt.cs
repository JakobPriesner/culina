namespace Api.Infrastructure;

/// <summary>
/// Marks an endpoint that is not subject to the session CSRF check.
/// </summary>
/// <remarks>
/// <para>
/// Only one endpoint carries this, and it needs a reason: <c>POST /sessions</c>.
/// The CSRF token is the thing that proves a request came from our own page, and
/// it lives in a readable cookie beside the <c>HttpOnly</c> session cookie. If
/// the readable one is lost while the session cookie survives — a browser that
/// clears non-HttpOnly cookies, an extension, a person clearing what their
/// developer tools let them clear — then every unsafe request fails, including
/// sign-out. The app is wedged, and "reload the page and try again" does not
/// help, because reloading does not bring the token back.
/// </para>
/// <para>
/// Signing in again is the way out, so signing in cannot be the thing that is
/// blocked. The request is still covered by the same-origin guard, which runs
/// first and rejects any unsafe request that did not come from this origin, so
/// a foreign page still cannot reach it.
/// </para>
/// </remarks>
internal sealed class CsrfExempt;
