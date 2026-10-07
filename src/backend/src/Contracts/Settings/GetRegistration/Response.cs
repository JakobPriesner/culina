namespace Contracts.Settings.GetRegistration;

/// <summary>
/// Who may create an account on this instance.
/// </summary>
/// <remarks>A contract type, not the settings record, so a future secret can be write-only.</remarks>
public sealed record Response
{
    /// <summary>Whether anyone may create an account.</summary>
    public required bool OpenRegistration { get; init; }

    /// <summary>Whether a new account must present an invitation code.</summary>
    public required bool RequireInvitation { get; init; }

    /// <summary>The largest number of accounts this instance allows.</summary>
    public required int MaxUsers { get; init; }
}
