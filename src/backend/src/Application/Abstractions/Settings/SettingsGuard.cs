namespace Application.Abstractions.Settings;

/// <summary>
/// The checks a bootstrap settings record runs at startup.
/// </summary>
/// <remarks>
/// These throw, and that is correct: a misconfigured process must fail to start
/// loudly rather than fail on the first request that happened to need the
/// value. It is a defect in the deployment, not a request outcome, so it is not
/// a <c>Result</c>. Every message names the environment variable, so the fix is
/// obvious from the log line alone.
/// </remarks>
public static class SettingsGuard
{
    /// <summary>Requires a value to be present and not whitespace.</summary>
    /// <param name="value">The configured value.</param>
    /// <param name="section">The configuration section name.</param>
    /// <param name="key">The key within the section.</param>
    public static void NotBlank(string? value, string section, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(section, key, "is required");
        }
    }

    /// <summary>Requires a value to fall within an inclusive range.</summary>
    /// <param name="value">The configured value.</param>
    /// <param name="minimum">The smallest acceptable value.</param>
    /// <param name="maximum">The largest acceptable value.</param>
    /// <param name="section">The configuration section name.</param>
    /// <param name="key">The key within the section.</param>
    public static void InRange(int value, int minimum, int maximum, string section, string key)
    {
        if (value < minimum || value > maximum)
        {
            throw Invalid(section, key, $"must be between {minimum} and {maximum}, but was {value}");
        }
    }

    /// <summary>Requires a path that the process can actually write to.</summary>
    /// <param name="path">The configured directory.</param>
    /// <param name="section">The configuration section name.</param>
    /// <param name="key">The key within the section.</param>
    public static void WritableDirectory(string path, string section, string key)
    {
        NotBlank(path, section, key);

        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Invalid(section, key, $"must be a writable directory, but '{path}' is not", exception);
        }
    }

    private static InvalidOperationException Invalid(
        string section,
        string key,
        string problem,
        Exception? cause = null) =>
        new($"Configuration {section}__{key} {problem}.", cause);
}
