using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Settings;

/// <summary>
/// Reads one configuration section key by key. Explicit reads, not reflection binding: a mistyped key is not silently defaulted
/// and every message names the environment variable.
/// </summary>
/// <param name="configuration">The configuration to read from.</param>
/// <param name="sectionName">The section prefix, for example <c>Database</c>.</param>
internal sealed class SettingsSection(IConfiguration configuration, string sectionName)
{
    internal string RequiredString(string key) =>
        configuration[Path(key)] is { Length: > 0 } value
            ? value
            : throw Missing(key);

    internal string String(string key, string fallback) =>
        configuration[Path(key)] is { Length: > 0 } value ? value : fallback;

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
