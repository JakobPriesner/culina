using Application.Abstractions.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Settings;

/// <summary>Reads and registers <see cref="ForwardedHeadersSettings"/>.</summary>
public static class ForwardedHeadersSettingsExtensions
{
    /// <summary>Binds the section, validates it, and registers it as a singleton.</summary>
    /// <param name="services">The container to register into.</param>
    /// <param name="configuration">The configuration to read from.</param>
    public static IServiceCollection AddForwardedHeadersSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = new SettingsSection(configuration, ForwardedHeadersSettings.SectionName);

        var settings = new ForwardedHeadersSettings
        {
            KnownProxies = section.CommaSeparated(nameof(ForwardedHeadersSettings.KnownProxies))
        };

        settings.Validate();

        return services.AddSingleton(settings);
    }
}
