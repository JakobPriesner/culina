using Application.Abstractions.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Settings;

/// <summary>Reads and registers <see cref="CookieSettings"/>.</summary>
public static class CookieSettingsExtensions
{
    /// <summary>Binds the section, validates it, and registers it as a singleton.</summary>
    /// <param name="services">The container to register into.</param>
    /// <param name="configuration">The configuration to read from.</param>
    /// <param name="environment">
    /// Where the process runs: only Development may turn secure cookies off
    /// without saying so.
    /// </param>
    public static IServiceCollection AddCookieSettings(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var section = new SettingsSection(configuration, CookieSettings.SectionName);

        var settings = new CookieSettings
        {
            Secure = section.Bool(nameof(CookieSettings.Secure), true),
            InsecureAllowed = environment.IsDevelopment()
                || section.Bool(CookieSettings.AllowInsecureKey, fallback: false),
            SessionDays = section.Int(nameof(CookieSettings.SessionDays), 30),
            RenewAfterHours = section.Int(nameof(CookieSettings.RenewAfterHours), 24)
        };

        settings.Validate();

        return services.AddSingleton(settings);
    }
}
