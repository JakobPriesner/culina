namespace Contracts.Settings.UpdateRegistration;

/// <summary>The registration policy to apply.</summary>
public sealed record Request
{
    /// <summary>Whether anyone may create an account.</summary>
    public required bool OpenRegistration { get; init; }

    /// <summary>Whether a new account must present an invitation code.</summary>
    public required bool RequireInvitation { get; init; }

    /// <summary>The largest number of accounts this instance allows.</summary>
    public required int MaxUsers { get; init; }
}
