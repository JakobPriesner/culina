namespace Application.Abstractions.Settings;

/// <summary>
/// Who may create an account on this instance.
/// </summary>
/// <remarks>
/// Settable properties, unlike the bootstrap records: an admin changes these
/// from the app and every consumer holding the singleton sees the new value
/// immediately.
/// </remarks>
public sealed record RegistrationSettings : IInstanceSettings<RegistrationSettings>
{
    /// <summary>The key this group is stored under.</summary>
    public static string GroupName => "registration";

    /// <summary>
    /// Whether anyone may create an account.
    /// </summary>
    /// <remarks>
    /// Closed by default. A self-hosted instance that opened registration the
    /// moment it came online would be discovered and filled with accounts
    /// before its owner finished setting it up.
    /// </remarks>
    public bool OpenRegistration { get; set; }

    /// <summary>Whether a new account must present an invitation code.</summary>
    public bool RequireInvitation { get; set; } = true;

    /// <summary>The largest number of accounts this instance allows.</summary>
    public int MaxUsers { get; set; } = 100;

    /// <inheritdoc/>
    public void CopyFrom(RegistrationSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        OpenRegistration = other.OpenRegistration;
        RequireInvitation = other.RequireInvitation;
        MaxUsers = other.MaxUsers;
    }
}
