using System.Globalization;
using Application.Abstractions.Settings;

namespace Application.Settings;

/// <summary>The database settings as configured, complete or not: read key by key because the host that asks runs <em>because</em> they are incomplete. Nothing here validates; saving does.</summary>
internal static class ConfiguredDatabase
{
    internal static DatabaseSettings Read(IServerConfiguration configuration) => new()
    {
        Host = Text(configuration, nameof(DatabaseSettings.Host)),
        Port = int.TryParse(Text(configuration, nameof(DatabaseSettings.Port)), CultureInfo.InvariantCulture, out var port)
            ? port
            : DatabaseSettings.DefaultPort,
        Name = Text(configuration, nameof(DatabaseSettings.Name)),
        Username = Text(configuration, nameof(DatabaseSettings.Username)),
        Password = Text(configuration, nameof(DatabaseSettings.Password)),
        RequireSsl = bool.TryParse(Text(configuration, nameof(DatabaseSettings.RequireSsl)), out var ssl)
            ? ssl
            : DatabaseSettings.DefaultRequireSsl,
        MaxPoolSize = int.TryParse(Text(configuration, nameof(DatabaseSettings.MaxPoolSize)), CultureInfo.InvariantCulture, out var pool)
            ? pool
            : DatabaseSettings.DefaultMaxPoolSize
    };

    private static string Text(IServerConfiguration configuration, string key) =>
        configuration.Read(SettingsKey.Of(DatabaseSettings.SectionName, key)) ?? string.Empty;
}
