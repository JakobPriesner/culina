using System.Globalization;
using Application.Abstractions.Settings;

namespace Application.Settings;

/// <summary>
/// The database settings as configured, whether or not they are complete.
/// </summary>
/// <remarks>
/// Read key by key rather than injected, because the host that asks for them
/// first is the one that runs <em>because</em> they are incomplete: it has no
/// validated <see cref="DatabaseSettings"/> to inject. A missing part reads as
/// empty, and nothing here validates — that is what saving does.
/// </remarks>
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
