using Api.Endpoints.Settings.GetRegistration.V1;
using Api.Endpoints.Settings.UpdateRegistration.V1;

namespace Api.Endpoints.Settings;

/// <summary>The instance-settings endpoints, registered explicitly.</summary>
internal static class SettingsEndpoints
{
    internal static IServiceCollection AddSettingsEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetRegistrationSettingsEndpoint>()
            .AddSingleton<IEndpoint, UpdateRegistrationSettingsEndpoint>();
}
