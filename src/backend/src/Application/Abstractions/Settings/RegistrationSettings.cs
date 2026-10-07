namespace Application.Abstractions.Settings;

/// <summary>Who may create an account on this instance. Mutable: an admin changes these and every holder of the singleton sees it.</summary>
public sealed record RegistrationSettings : IInstanceSettings<RegistrationSettings>
{
    /// <summary>The key this group is stored under.</summary>
    public static string GroupName => "registration";

    /// <summary>Whether anyone may create an account. Closed by default, so a new instance is not filled before its owner finishes setup.</summary>
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
