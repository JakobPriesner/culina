namespace Contracts.Users.UpdatePreferences;

/// <summary>How this person wants the app to look and read.</summary>
public sealed record Response
{
    /// <summary>The language they read in.</summary>
    public required string Locale { get; init; }

    /// <summary>The theme id.</summary>
    public required string Theme { get; init; }

    /// <summary>Light, dark, or follow the device.</summary>
    public required string Mode { get; init; }

    /// <summary>Which units to render.</summary>
    public required string MeasurementSystem { get; init; }

    /// <summary>The entity version.</summary>
    public required long Version { get; init; }
}
