namespace Api.Infrastructure;

/// <summary>Marks an endpoint that is not subject to the session CSRF check.</summary>
/// <remarks>
/// Only the ways back in: sign-in and recovery-code password reset. If the readable CSRF cookie is lost while the
/// session cookie survives, every unsafe request fails, so signing in cannot be blocked. A foreign page is kept from
/// logging a browser in by these endpoints accepting only <c>application/json</c>, which a cross-site form cannot send
/// and a CORS preflight (never granted) would be needed for. A test pins this for sign-in.
/// </remarks>
internal sealed class CsrfExempt;
