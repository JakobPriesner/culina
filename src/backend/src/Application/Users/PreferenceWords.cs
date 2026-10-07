using Domain.Shared;
using Domain.Users;

namespace Application.Users;

/// <summary>Parses preference codes off the wire (the codes live in <see cref="PreferenceCodes"/>), naming the wrong field for the form.</summary>
internal static class PreferenceWords
{
    // Checks a language choice; <c>system</c> is valid but has no Language to parse to.
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
