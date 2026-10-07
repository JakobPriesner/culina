namespace Application.Abstractions.Settings;

/// <summary>Where the state that must outlive a container lives; both paths need persisted volumes (images, and the key ring whose loss signs everyone out).</summary>
public sealed record StorageSettings
{
    /// <summary>The configuration section these values are read from.</summary>
    public const string SectionName = "Storage";

    /// <summary>The directory holding re-encoded recipe images.</summary>
    public required string ImagePath { get; init; }

    /// <summary>The directory holding the ASP.NET data-protection key ring.</summary>
    public required string DataProtectionKeyPath { get; init; }

    /// <summary>Where <see cref="ConfigPath"/> points when nothing says otherwise.</summary>
    public const string DefaultConfigPath = "/data/config";

    /// <summary>The directory for settings saved from the app. Need not be writable: environment-only instances must still start.</summary>
    public string ConfigPath { get; init; } = DefaultConfigPath;

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
