using Application.Abstractions.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Settings;

/// <summary>Reads and registers <see cref="StorageSettings"/>.</summary>
public static class StorageSettingsExtensions
{
    /// <summary>Binds the section, validates it, and registers it as a singleton.</summary>
    /// <param name="services">The container to register into.</param>
    /// <param name="configuration">The configuration to read from.</param>
    public static IServiceCollection AddStorageSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = new SettingsSection(configuration, StorageSettings.SectionName);

        var settings = new StorageSettings
        {
            ImagePath = section.String(nameof(StorageSettings.ImagePath), "/data/images"),
            DataProtectionKeyPath = section.String(
                nameof(StorageSettings.DataProtectionKeyPath),
                "/data/keys"),
            MaxImageBytes = section.Int(nameof(StorageSettings.MaxImageBytes), 10 * 1024 * 1024)
        };

        settings.Validate();

        return services.AddSingleton(settings);
    }
}
