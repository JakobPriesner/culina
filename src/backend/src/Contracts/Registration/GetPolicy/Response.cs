namespace Contracts.Registration.GetPolicy;

/// <summary>
/// What a sign-up form needs to know before it draws itself.
/// </summary>
/// <remarks>Public and deliberately narrower than the administrator's view: it says what an account may do, not how the instance is configured.</remarks>
public sealed record Response
{
    /// <summary>Whether anyone may create an account.</summary>
    public required bool OpenRegistration { get; init; }

    /// <summary>Whether a new account must present an invitation code.</summary>
    public required bool RequireInvitation { get; init; }

    /// <summary>
    /// Whether this instance has been set up yet.
    /// </summary>
    public required bool HasAccounts { get; init; }
}
