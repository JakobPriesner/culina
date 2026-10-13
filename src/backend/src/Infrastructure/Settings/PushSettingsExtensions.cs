using Application.Abstractions.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Settings;

/// <summary>Reads and registers <see cref="PushSettings"/>.</summary>
public static class PushSettingsExtensions
{
    /// <summary>Reads <c>Push:RelayUrl</c>, validates it, and registers the settings as a singleton.</summary>
    public static IServiceCollection AddPushSettings(this IServiceCollection services, IConfiguration configuration)
    {
        var raw = new SettingsSection(configuration, PushSettings.SectionName).String(nameof(PushSettings.RelayUrl), string.Empty);

        var settings = new PushSettings
        {
            RelayUrl = raw.Length == 0
                ? null
                : Uri.TryCreate(raw, UriKind.Absolute, out var relay)
                    ? relay
                    : throw new InvalidOperationException(
                        $"Configuration Push__RelayUrl must be an https:// address, but was '{raw}'.")
        };

        settings.Validate();

        return services.AddSingleton(settings);
    }
}
