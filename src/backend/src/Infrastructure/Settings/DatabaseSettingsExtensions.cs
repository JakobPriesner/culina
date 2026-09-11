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
            Port = section.Int(nameof(DatabaseSettings.Port), 5432),
            Name = section.RequiredString(nameof(DatabaseSettings.Name)),
            Username = section.RequiredString(nameof(DatabaseSettings.Username)),
            Password = section.RequiredString(nameof(DatabaseSettings.Password)),
            RequireSsl = section.Bool(nameof(DatabaseSettings.RequireSsl), true),
            MaxPoolSize = section.Int(nameof(DatabaseSettings.MaxPoolSize), 20)
        };

        settings.Validate();

        return services.AddSingleton(settings);
    }
}
