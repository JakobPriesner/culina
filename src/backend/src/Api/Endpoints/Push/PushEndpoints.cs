namespace Api.Endpoints.Push;

/// <summary>The notification endpoints, registered explicitly.</summary>
internal static class PushEndpoints
{
    internal static IServiceCollection AddPushEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetKey.V1.GetPushKeyEndpoint>()
            .AddSingleton<IEndpoint, RegisterSubscription.V1.RegisterPushEndpoint>()
            .AddSingleton<IEndpoint, RemoveSubscription.V1.RemovePushEndpoint>()
            .AddSingleton<IEndpoint, GetRelay.V1.GetPushRelayEndpoint>()
            .AddSingleton<IEndpoint, RegisterDevice.V1.RegisterPushDeviceEndpoint>()
            .AddSingleton<IEndpoint, RemoveDevice.V1.RemovePushDeviceEndpoint>();
}
