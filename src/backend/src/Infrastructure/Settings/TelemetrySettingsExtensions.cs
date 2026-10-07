using Application.Abstractions.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Settings;

/// <summary>Reads and registers <see cref="TelemetrySettings"/>.</summary>
public static class TelemetrySettingsExtensions
{
    /// <summary>
    /// Reads the two OpenTelemetry keys, validates them, and registers them as a singleton.
    /// </summary>
    /// <summary>
    /// Reads the two OpenTelemetry keys (which the exporter reads itself), validates them, and
    /// registers them as a singleton.
    /// </summary>
    public static IServiceCollection AddTelemetrySettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = new TelemetrySettings
        {
            Endpoint = Endpoint(configuration[TelemetrySettings.EndpointKey]),
            Protocol = configuration[TelemetrySettings.ProtocolKey] is { Length: > 0 } protocol
                ? protocol
                : TelemetrySettings.Grpc
        };

        settings.Validate();

        return services.AddSingleton(settings);
    }

    private static Uri? Endpoint(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return Uri.TryCreate(raw, UriKind.Absolute, out var endpoint)
            ? endpoint
            : throw new InvalidOperationException(
                $"Configuration {TelemetrySettings.EndpointKey} must be an http:// or https:// address, but was '{raw}'.");
    }
}
