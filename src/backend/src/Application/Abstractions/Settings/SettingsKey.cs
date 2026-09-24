namespace Application.Abstractions.Settings;

/// <summary>
/// The two spellings of a configuration key.
/// </summary>
/// <remarks>
/// Built from a record's <c>SectionName</c> and a property name, the same way
/// the settings extensions read them, so the key a handler writes is the key
/// the next startup reads.
/// </remarks>
public static class SettingsKey
{
    /// <summary>The configuration's own form: <c>Cookies:Secure</c>.</summary>
    /// <param name="section">The record's section, or empty for a root key.</param>
    /// <param name="key">The key within it.</param>
    public static string Of(string section, string key) =>
        string.IsNullOrEmpty(section) ? key : $"{section}:{key}";

    /// <summary>
    /// The environment variable that sets it: <c>Cookies__Secure</c>. What a
    /// person has to find in their deployment, so what the screen names.
    /// </summary>
    /// <param name="key">A key in the configuration's own form.</param>
    public static string Variable(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return key.Replace(":", "__", StringComparison.Ordinal);
    }
}
