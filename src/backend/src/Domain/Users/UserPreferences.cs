using Domain.Shared;

namespace Domain.Users;

/// <summary>Whether to follow the device or force one appearance.</summary>
public enum ThemeMode
{
    /// <summary>Follow the operating system.</summary>
    System = 0,

    /// <summary>Always light.</summary>
    Light = 1,

    /// <summary>Always dark.</summary>
    Dark = 2
}

/// <summary>Which units amounts are shown in.</summary>
public enum MeasurementSystem
{
    /// <summary>Grams, millilitres, Celsius.</summary>
    Metric = 0,

    /// <summary>
    /// Ounces, cups, Fahrenheit. Stored from the start but not yet rendered:
    /// converting honestly is harder than it looks, so v1 always shows metric.
    /// </summary>
    Imperial = 1
}

/// <summary>
/// How one person wants the app to look and read.
/// </summary>
/// <remarks>
/// Person-owned, never household-owned: two people sharing a kitchen do not
/// share an appetite for dark mode.
/// </remarks>
public sealed class UserPreferences
{
    /// <summary>The longest theme id the database column accepts.</summary>
    public const int MaxThemeLength = 40;

    /// <summary>The theme every instance ships with.</summary>
    public const string DefaultTheme = "warm-paper";

    private UserPreferences(
        Guid userId,
        Language language,
        string theme,
        ThemeMode mode,
        MeasurementSystem measurementSystem,
        long version)
    {
        UserId = userId;
        Language = language;
        Theme = theme;
        Mode = mode;
        MeasurementSystem = measurementSystem;
        Version = version;
    }

    /// <summary>Whose preferences these are.</summary>
    public Guid UserId { get; }

    /// <summary>The language they read the interface in.</summary>
    public Language Language { get; private set; }

    /// <summary>
    /// The theme id. A free string rather than an enum, because themes are
    /// files in the frontend and the backend has no business enumerating them.
    /// </summary>
    public string Theme { get; private set; }

    /// <summary>Light, dark, or follow the device.</summary>
    public ThemeMode Mode { get; private set; }

    /// <summary>Which units to render.</summary>
    public MeasurementSystem MeasurementSystem { get; private set; }

    /// <summary>Incremented by every write.</summary>
    public long Version { get; private set; }

    /// <summary>The preferences a new account starts with.</summary>
    /// <param name="userId">Whose they are.</param>
    /// <param name="language">Their language, usually guessed from Accept-Language.</param>
    public static UserPreferences Default(Guid userId, Language language = Language.En) =>
        new(userId, language, DefaultTheme, ThemeMode.System, MeasurementSystem.Metric, version: 1);

    /// <summary>Rebuilds preferences from storage.</summary>
    /// <param name="userId">Whose they are.</param>
    /// <param name="language">Their language.</param>
    /// <param name="theme">Their theme id.</param>
    /// <param name="mode">Their appearance choice.</param>
    /// <param name="measurementSystem">Their unit choice.</param>
    /// <param name="version">The stored version.</param>
    public static UserPreferences Restore(
        Guid userId,
        Language language,
        string theme,
        ThemeMode mode,
        MeasurementSystem measurementSystem,
        long version) =>
        new(userId, language, theme, mode, measurementSystem, version);

    /// <summary>Applies a change.</summary>
    /// <param name="language">The chosen language.</param>
    /// <param name="theme">The chosen theme id.</param>
    /// <param name="mode">The chosen appearance.</param>
    /// <param name="measurementSystem">The chosen units.</param>
    public Result Change(
        Language language,
        string theme,
        ThemeMode mode,
        MeasurementSystem measurementSystem)
    {
        var trimmed = theme?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxThemeLength)
        {
            return UserErrors.InvalidTheme;
        }

        Language = language;
        Theme = trimmed;
        Mode = mode;
        MeasurementSystem = measurementSystem;

        return Result.Success();
    }
}
