using Domain.Shared;

namespace Domain.Users;

/// <summary>
/// The canonical lowercase spelling of every preference value.
/// </summary>
/// <remarks>
/// One definition, used by the wire contract and by the storage mapper alike.
/// They were briefly two identical switch expressions in two layers, which is
/// how "system" and "System" become different values in three places.
/// </remarks>
public static class PreferenceCodes
{
    /// <summary>The code for following the device, for a language or an appearance.</summary>
    public const string System = "system";

    /// <summary>The code for a language, or <c>system</c> when it follows the device.</summary>
    public static string Of(Language? language) => language switch
    {
        null => System,
        Language.De => "de",
        _ => "en"
    };

    /// <summary>The code for an appearance choice.</summary>
    public static string Of(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => "light",
        ThemeMode.Dark => "dark",
        _ => System
    };

    /// <summary>The code for a unit system.</summary>
    public static string Of(MeasurementSystem system) => system switch
    {
        MeasurementSystem.Imperial => "imperial",
        _ => "metric"
    };

    /// <summary>
    /// Reads a language code, or null when it is not one — <c>system</c>
    /// included, which leaves the language to the device.
    /// </summary>
    public static Language? ToLanguage(string? code) => code switch
    {
        "en" => Language.En,
        "de" => Language.De,
        _ => null
    };

    /// <summary>Reads an appearance code, or null when it is not one.</summary>
    public static ThemeMode? ToMode(string? code) => code switch
    {
        "light" => ThemeMode.Light,
        "dark" => ThemeMode.Dark,
        System => ThemeMode.System,
        _ => null
    };

    /// <summary>Reads a unit-system code, or null when it is not one.</summary>
    public static MeasurementSystem? ToMeasurementSystem(string? code) => code switch
    {
        "metric" => MeasurementSystem.Metric,
        "imperial" => MeasurementSystem.Imperial,
        _ => null
    };
}
