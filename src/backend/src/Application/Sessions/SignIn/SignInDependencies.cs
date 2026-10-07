using Application.Abstractions.Settings;

namespace Application.Sessions.SignIn;

/// <summary>The ambient values signing in needs (the clock and cookie lifetime), grouped to keep the handler's constructor about its collaborators.</summary>
/// <param name="Time">The injected clock.</param>
/// <param name="Cookies">How long a session may live.</param>
public sealed record SignInDependencies(TimeProvider Time, CookieSettings Cookies);
