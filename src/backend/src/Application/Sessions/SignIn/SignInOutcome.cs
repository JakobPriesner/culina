using Response = Contracts.Sessions.SignIn.Response;

namespace Application.Sessions.SignIn;

/// <summary>What signing in produces: a body, and a secret that must not be in it.</summary>
/// <param name="Response">What the client receives as JSON.</param>
/// <param name="SessionToken">The value for the <c>HttpOnly</c> session cookie.</param>
/// <remarks>
/// The one handler that does not return a Contracts type directly: the token must leave only as a
/// cookie scripts cannot read, so it is carried beside the response and only the endpoint sees
/// both.
/// </remarks>
public sealed record SignInOutcome(Response Response, string SessionToken);
