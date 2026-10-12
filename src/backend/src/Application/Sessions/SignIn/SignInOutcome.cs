using Domain.Sessions;
using Response = Contracts.Sessions.SignIn.Response;

namespace Application.Sessions.SignIn;

/// <summary>What signing in produces: a body, and a secret that must not be in it.</summary>
/// <param name="Response">What the client receives as JSON.</param>
/// <param name="SessionToken">The session token: the <c>HttpOnly</c> cookie for a browser, the bearer token for an app.</param>
/// <param name="Client">What the session was started from, which decides how the token leaves.</param>
/// <remarks>
/// The one handler that does not return a Contracts type directly: for a browser the token must
/// leave only as a cookie scripts cannot read, so it is carried beside the response and only the
/// endpoint decides where it goes.
/// </remarks>
public sealed record SignInOutcome(Response Response, string SessionToken, SessionClient Client);
