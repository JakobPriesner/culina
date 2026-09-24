using Api.Endpoints.Settings.GetAssistance.V1;
using Api.Endpoints.Settings.GetAssistanceModels.V1;
using Api.Endpoints.Settings.GetAssistanceUsage.V1;
using Api.Endpoints.Settings.GetRegistration.V1;
using Api.Endpoints.Settings.GetServer.V1;
using Api.Endpoints.Settings.UpdateAssistance.V1;
using Api.Endpoints.Settings.UpdateRegistration.V1;
using Api.Endpoints.Settings.UpdateServer.V1;

namespace Api.Endpoints.Settings;

/// <summary>The instance-settings endpoints, registered explicitly.</summary>
internal static class SettingsEndpoints
{
    internal static IServiceCollection AddSettingsEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetRegistrationSettingsEndpoint>()
            .AddSingleton<IEndpoint, UpdateRegistrationSettingsEndpoint>()
            .AddSingleton<IEndpoint, GetAssistanceSettingsEndpoint>()
            .AddSingleton<IEndpoint, UpdateAssistanceSettingsEndpoint>()
            .AddSingleton<IEndpoint, GetAssistanceUsageEndpoint>()
            .AddSingleton<IEndpoint, GetAssistanceModelsEndpoint>()
            // The database settings are registered with the setup endpoints,
            // because the host with no database serves them too.
            .AddSingleton<IEndpoint, GetServerSettingsEndpoint>()
            .AddSingleton<IEndpoint, UpdateServerSettingsEndpoint>();
}
