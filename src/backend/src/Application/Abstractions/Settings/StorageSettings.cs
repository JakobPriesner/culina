namespace Application.Abstractions.Settings;

/// <summary>
/// Where the two pieces of state that must outlive a container live.
/// </summary>
/// <remarks>
/// Both paths must be on persisted volumes. Losing <see cref="ImagePath"/>
/// loses every recipe photo on redeploy; losing
/// <see cref="DataProtectionKeyPath"/> invalidates every session cookie on
/// restart, silently signing every user out.
/// </remarks>
public sealed record StorageSettings
{
    /// <summary>The configuration section these values are read from.</summary>
    public const string SectionName = "Storage";

    /// <summary>The directory holding re-encoded recipe images.</summary>
    public required string ImagePath { get; init; }

    /// <summary>The directory holding the ASP.NET data-protection key ring.</summary>
    public required string DataProtectionKeyPath { get; init; }

    /// <summary>The largest upload accepted, in bytes.</summary>
    public int MaxImageBytes { get; init; } = 10 * 1024 * 1024;

    /// <summary>Throws when any value would make the process unable to serve.</summary>
    public void Validate()
    {
        SettingsGuard.WritableDirectory(ImagePath, SectionName, nameof(ImagePath));
        SettingsGuard.WritableDirectory(
            DataProtectionKeyPath,
            SectionName,
            nameof(DataProtectionKeyPath));
        SettingsGuard.InRange(MaxImageBytes, 1024, 50 * 1024 * 1024, SectionName, nameof(MaxImageBytes));
    }
}
