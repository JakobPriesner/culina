using Application.Abstractions.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Settings;

/// <summary>Reads and registers <see cref="SiteSettings"/>.</summary>
public static class SiteSettingsExtensions
{
    /// <summary>Binds the section, validates it, and registers it as a singleton.</summary>
    /// <param name="services">The container to register into.</param>
    /// <param name="configuration">The configuration to read from.</param>
    public static IServiceCollection AddSiteSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = new SiteSettings
        {
            Url = Address(configuration, nameof(SiteSettings.Url)),
            SecurityContact = Address(configuration, nameof(SiteSettings.SecurityContact))
        };

        settings.Validate();

        return services.AddSingleton(settings);
    }

    private static Uri? Address(IConfiguration configuration, string key)
    {
        var raw = configuration[$"{SiteSettings.SectionName}:{key}"];

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var address)
            ? address
            : throw new InvalidOperationException(
                $"Configuration {SiteSettings.SectionName}__{key} must be an absolute address, but was '{raw}'.");
    }
}
