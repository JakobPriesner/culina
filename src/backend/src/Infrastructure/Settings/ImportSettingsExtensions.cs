using Application.Abstractions.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Settings;

/// <summary>Reads and registers <see cref="ImportSettings"/>.</summary>
public static class ImportSettingsExtensions
{
    /// <summary>Binds the section, validates it, and registers it as a singleton.</summary>
    /// <param name="services">The container to register into.</param>
    /// <param name="configuration">The configuration to read from.</param>
    public static IServiceCollection AddImportSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = new SettingsSection(configuration, ImportSettings.SectionName);

        var settings = new ImportSettings
        {
            AllowPrivateSourceAddresses = section.Bool(
                nameof(ImportSettings.AllowPrivateSourceAddresses),
                fallback: false)
        };

        ImportSettings.Validate();

        return services.AddSingleton(settings);
    }
}
