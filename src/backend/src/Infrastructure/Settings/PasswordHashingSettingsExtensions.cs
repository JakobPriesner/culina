using Application.Abstractions.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Settings;

/// <summary>Reads and registers <see cref="PasswordHashingSettings"/>.</summary>
public static class PasswordHashingSettingsExtensions
{
    /// <summary>Binds the section, validates it, and registers it as a singleton.</summary>
    /// <param name="services">The container to register into.</param>
    /// <param name="configuration">The configuration to read from.</param>
    public static IServiceCollection AddPasswordHashingSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = new SettingsSection(configuration, PasswordHashingSettings.SectionName);

        var settings = new PasswordHashingSettings
        {
            MemoryKib = section.Int(nameof(PasswordHashingSettings.MemoryKib), 65536),
            Iterations = section.Int(nameof(PasswordHashingSettings.Iterations), 3),
            Parallelism = section.Int(nameof(PasswordHashingSettings.Parallelism), 2)
        };

        settings.Validate();

        return services.AddSingleton(settings);
    }
}
