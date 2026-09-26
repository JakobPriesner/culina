using Domain.Shared;
using Domain.Users;

namespace Application.Users;

/// <summary>
/// Parses preference codes off the wire, naming the field that was wrong.
/// </summary>
/// <remarks>
/// The codes themselves live in <see cref="PreferenceCodes"/>; this only adds
/// the field labelling a form needs to mark the right control.
/// </remarks>
internal static class PreferenceWords
{
    /// <summary>
    /// Checks a language choice. <c>system</c> is one, but has no
    /// <see cref="Language"/> to parse to: it leaves the language to whichever
    /// device is reading.
    /// </summary>
    internal static Result CheckLanguage(string? value) =>
        value == PreferenceCodes.System || PreferenceCodes.ToLanguage(value) is not null
            ? Result.Success()
            : Invalid("locale", "Locale must be 'system', 'en' or 'de'.");

    internal static Result<ThemeMode> ToMode(string? value) =>
        PreferenceCodes.ToMode(value) is { } mode
            ? mode
            : Invalid("mode", "Mode must be 'light', 'dark' or 'system'.");

    internal static Result<MeasurementSystem> ToMeasurementSystem(string? value) =>
        PreferenceCodes.ToMeasurementSystem(value) is { } system
            ? system
            : Invalid("measurementSystem", "Measurement system must be 'metric' or 'imperial'.");

    private static FieldError Invalid(string field, string detail) =>
        new(field, UserErrors.InvalidPreference.Code, detail);
}
