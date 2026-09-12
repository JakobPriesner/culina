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
    internal static Result<Language> ToLanguage(string? value) =>
        PreferenceCodes.ToLanguage(value) is { } language
            ? language
            : Invalid("locale", "Locale must be 'en' or 'de'.");

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
