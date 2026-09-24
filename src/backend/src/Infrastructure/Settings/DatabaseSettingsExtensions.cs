using Application.Abstractions.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Settings;

/// <summary>Reads and registers <see cref="DatabaseSettings"/>.</summary>
public static class DatabaseSettingsExtensions
{
    /// <summary>Binds the section, validates it, and registers it as a singleton.</summary>
    /// <param name="services">The container to register into.</param>
    /// <param name="configuration">The configuration to read from.</param>
    public static IServiceCollection AddDatabaseSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = new SettingsSection(configuration, DatabaseSettings.SectionName);

        var settings = new DatabaseSettings
        {
            Host = section.RequiredString(nameof(DatabaseSettings.Host)),
            Port = section.Int(nameof(DatabaseSettings.Port), DatabaseSettings.DefaultPort),
            Name = section.RequiredString(nameof(DatabaseSettings.Name)),
            Username = section.RequiredString(nameof(DatabaseSettings.Username)),
            Password = section.RequiredString(nameof(DatabaseSettings.Password)),
            RequireSsl = section.Bool(nameof(DatabaseSettings.RequireSsl), DatabaseSettings.DefaultRequireSsl),
            MaxPoolSize = section.Int(nameof(DatabaseSettings.MaxPoolSize), DatabaseSettings.DefaultMaxPoolSize)
        };

        settings.Validate();

        return services.AddSingleton(settings);
    }

    /// <summary>
    /// Whether every part without a default has been given, from anywhere.
    /// </summary>
    /// <param name="configuration">The configuration to read from.</param>
    /// <remarks>
    /// The question that decides which host runs. With all four, the app starts
    /// and <see cref="AddDatabaseSettings"/> validates them as it always did —
    /// a wrong one is still a failed start, not a setup screen, because a
    /// database that is down for a minute must not hand the instance to whoever
    /// opens it next. With any missing, nobody ever configured one, and the
    /// setup host asks for it.
    /// </remarks>
    public static bool IsDatabaseConfigured(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string[] required =
        [
            nameof(DatabaseSettings.Host),
            nameof(DatabaseSettings.Name),
            nameof(DatabaseSettings.Username),
            nameof(DatabaseSettings.Password)
        ];

        return required.All(key =>
            !string.IsNullOrWhiteSpace(configuration[SettingsKey.Of(DatabaseSettings.SectionName, key)]));
    }
}
