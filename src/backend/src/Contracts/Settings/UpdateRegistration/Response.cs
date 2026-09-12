namespace Contracts.Settings.UpdateRegistration;

/// <summary>
/// Who may create an account on this instance.
/// </summary>
/// <remarks>
/// A contract type rather than the settings record serialised directly. That is
/// what will let a future secret be write-only — an api key going in, and
/// <c>apiKeyConfigured: true</c> coming back out, never the value.
/// </remarks>
public sealed record Response
{
    /// <summary>Whether anyone may create an account.</summary>
    public required bool OpenRegistration { get; init; }

    /// <summary>Whether a new account must present an invitation code.</summary>
    public required bool RequireInvitation { get; init; }

    /// <summary>The largest number of accounts this instance allows.</summary>
    public required int MaxUsers { get; init; }
}
