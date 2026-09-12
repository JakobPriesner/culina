namespace Domain.Shared;

/// <summary>Failures when changing instance settings.</summary>
public static class SettingsErrors
{
    /// <summary>A setting was given a value it cannot take.</summary>
    public static readonly Error InvalidValue = new(
        "settings.invalid_value",
        "That is not a value this setting accepts.",
        ErrorType.Validation);
}
