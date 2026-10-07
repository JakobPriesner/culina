namespace Application.Abstractions.Settings;

/// <summary>The two spellings of a configuration key, built as the settings extensions read them so a handler's key is the next startup's key.</summary>
public static class SettingsKey
{
    /// <summary>The configuration's own form: <c>Cookies:Secure</c>.</summary>
    /// <param name="section">The record's section, or empty for a root key.</param>
    /// <param name="key">The key within it.</param>
    public static string Of(string section, string key) =>
        string.IsNullOrEmpty(section) ? key : $"{section}:{key}";

    /// <summary>The environment variable that sets it (<c>Cookies__Secure</c>): what a person finds in their deployment.</summary>
    /// <param name="key">A key in the configuration's own form.</param>
    public static string Variable(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return key.Replace(":", "__", StringComparison.Ordinal);
    }
}
