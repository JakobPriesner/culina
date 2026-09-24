using Api.Endpoints.Settings.GetDatabase.V1;
using Api.Endpoints.Settings.UpdateDatabase.V1;
using Api.Endpoints.Setup.Get.V1;

namespace Api.Endpoints.Setup;

/// <summary>
/// The endpoints the host that runs before there is a database serves, and the
/// real host serves too.
/// </summary>
internal static class SetupEndpoints
{
    internal static IServiceCollection AddSetupEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetSetupEndpoint>()
            .AddSingleton<IEndpoint, GetDatabaseSettingsEndpoint>()
            .AddSingleton<IEndpoint, UpdateDatabaseSettingsEndpoint>();
}
