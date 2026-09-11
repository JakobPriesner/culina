using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Settings;

/// <summary>
/// Reads one configuration section key by key.
/// </summary>
/// <remarks>
/// Explicit reads rather than <c>GetSection(...).Get&lt;T&gt;()</c>: binding by
/// reflection needs a binder in the startup path, silently leaves a mistyped
/// key at its default, and reports "could not bind" instead of naming the key
/// that is wrong. Every message here names the environment variable, so the fix
/// is obvious from the log line.
/// </remarks>
/// <param name="configuration">The configuration to read from.</param>
/// <param name="sectionName">The section prefix, for example <c>Database</c>.</param>
internal sealed class SettingsSection(IConfiguration configuration, string sectionName)
{
    /// <summary>Reads a value that has no safe default.</summary>
    /// <param name="key">The key within the section.</param>
    internal string RequiredString(string key) =>
        configuration[Path(key)] is { Length: > 0 } value
            ? value
            : throw Missing(key);

    /// <summary>Reads a value, falling back when it is absent.</summary>
    /// <param name="key">The key within the section.</param>
    /// <param name="fallback">Used when the key is absent or empty.</param>
    internal string String(string key, string fallback) =>
        configuration[Path(key)] is { Length: > 0 } value ? value : fallback;

    /// <summary>Reads an integer, falling back when it is absent.</summary>
    /// <param name="key">The key within the section.</param>
    /// <param name="fallback">Used when the key is absent or empty.</param>
    internal int Int(string key, int fallback)
    {
        if (configuration[Path(key)] is not { Length: > 0 } raw)
        {
            return fallback;
        }

        return int.TryParse(raw, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw NotA(key, raw, "whole number");
    }

    /// <summary>Reads a boolean, falling back when it is absent.</summary>
    /// <param name="key">The key within the section.</param>
    /// <param name="fallback">Used when the key is absent or empty.</param>
    internal bool Bool(string key, bool fallback)
    {
        if (configuration[Path(key)] is not { Length: > 0 } raw)
        {
            return fallback;
        }

        return bool.TryParse(raw, out var parsed)
            ? parsed
            : throw NotA(key, raw, "boolean (true or false)");
    }

    /// <summary>Reads a comma-separated list, trimming and dropping blanks.</summary>
    /// <param name="key">The key within the section.</param>
    internal IReadOnlyList<string> CommaSeparated(string key) =>
        configuration[Path(key)] is not { Length: > 0 } raw
            ? []
            : [.. raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];

    private string Path(string key) => $"{sectionName}:{key}";

    private InvalidOperationException Missing(string key) =>
        new($"Configuration {sectionName}__{key} is required but was not set.");

    private InvalidOperationException NotA(string key, string raw, string expected) =>
        new($"Configuration {sectionName}__{key} must be a {expected}, but was '{raw}'.");
}
