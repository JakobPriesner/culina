using Application.Abstractions.Settings;

namespace Application.Users.Register;

/// <summary>
/// The ambient values registration needs: the clock and the instance's policy.
/// </summary>
/// <remarks>
/// Grouped so the handler's constructor stays about the things it actually
/// orchestrates. A clock and a settings record are not collaborators, and
/// listing them beside the repositories makes a five-collaborator use case look
/// like a seven-collaborator one.
/// </remarks>
/// <param name="Time">The injected clock.</param>
/// <param name="Registration">Who may create an account.</param>
public sealed record RegistrationDependencies(
    TimeProvider Time,
    RegistrationSettings Registration);
