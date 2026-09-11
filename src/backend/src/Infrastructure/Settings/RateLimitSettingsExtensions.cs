using Application.Abstractions.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Settings;

/// <summary>Reads and registers <see cref="RateLimitSettings"/>.</summary>
public static class RateLimitSettingsExtensions
{
    /// <summary>Binds the section, validates it, and registers it as a singleton.</summary>
    /// <param name="services">The container to register into.</param>
    /// <param name="configuration">The configuration to read from.</param>
    public static IServiceCollection AddRateLimitSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = new SettingsSection(configuration, RateLimitSettings.SectionName);

        var settings = new RateLimitSettings
        {
            LoginPerIpPerMinute = section.Int(nameof(RateLimitSettings.LoginPerIpPerMinute), 10),
            LoginPerAccountPerMinute = section.Int(nameof(RateLimitSettings.LoginPerAccountPerMinute), 5),
            RegisterPerIpPerHour = section.Int(nameof(RateLimitSettings.RegisterPerIpPerHour), 5),
            InvitationPerIpPerHour = section.Int(nameof(RateLimitSettings.InvitationPerIpPerHour), 10),
            RequestsPerSessionPerMinute = section.Int(
                nameof(RateLimitSettings.RequestsPerSessionPerMinute),
                600)
        };

        settings.Validate();

        return services.AddSingleton(settings);
    }
}
