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
    /// <summary>The code for a language.</summary>
    public static string Of(Locale locale) => locale switch
    {
        Locale.De => "de",
        _ => "en"
    };

    /// <summary>The code for an appearance choice.</summary>
    public static string Of(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => "light",
        ThemeMode.Dark => "dark",
        _ => "system"
    };

    /// <summary>The code for a unit system.</summary>
    public static string Of(MeasurementSystem system) => system switch
    {
        MeasurementSystem.Imperial => "imperial",
        _ => "metric"
    };

    /// <summary>Reads a language code, or null when it is not one.</summary>
    public static Locale? ToLocale(string? code) => code switch
    {
        "en" => Locale.En,
        "de" => Locale.De,
        _ => null
    };

    /// <summary>Reads an appearance code, or null when it is not one.</summary>
    public static ThemeMode? ToMode(string? code) => code switch
    {
        "light" => ThemeMode.Light,
        "dark" => ThemeMode.Dark,
        "system" => ThemeMode.System,
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
