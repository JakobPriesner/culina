using Response = Contracts.Sessions.SignIn.Response;

namespace Application.Sessions.SignIn;

/// <summary>
/// What signing in produces: a body, and a secret that must not be in it.
/// </summary>
/// <param name="Response">What the client receives as JSON.</param>
/// <param name="SessionToken">
/// The value for the <c>HttpOnly</c> session cookie.
/// </param>
/// <remarks>
/// This is the one operation whose handler does not return a Contracts type
/// directly, and the reason is deliberate: the session token is an output of
/// the use case but must leave only as a cookie the browser's scripts cannot
/// read. Returning it inside the response record would make putting it in the
/// body the default mistake, so it is carried beside the response and the
/// endpoint is the only thing that ever sees both.
/// </remarks>
public sealed record SignInOutcome(Response Response, string SessionToken);
