using Application.Abstractions.Settings;

namespace Application.Users.Register;

/// <summary>The ambient values registration needs (clock and policy), grouped to keep the handler's collaborators few.</summary>
/// <param name="Time">The injected clock.</param>
/// <param name="Registration">Who may create an account.</param>
public sealed record RegistrationDependencies(
    TimeProvider Time,
    RegistrationSettings Registration);
