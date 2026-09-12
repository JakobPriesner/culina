using Application.Abstractions.Settings;

namespace Application.Sessions.SignIn;

/// <summary>
/// The ambient values signing in needs: the clock and the cookie lifetime.
/// </summary>
/// <remarks>
/// Grouped so the handler's constructor stays about its collaborators. They are
/// settings and a clock, not services the use case orchestrates, and separating
/// them keeps the parameter list honest about what the handler actually talks
/// to.
/// </remarks>
/// <param name="Time">The injected clock.</param>
/// <param name="Cookies">How long a session may live.</param>
public sealed record SignInDependencies(TimeProvider Time, CookieSettings Cookies);
