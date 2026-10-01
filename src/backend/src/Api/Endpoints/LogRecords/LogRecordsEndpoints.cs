using Api.Endpoints.LogRecords.Create.V1;

namespace Api.Endpoints.LogRecords;

/// <summary>The log records domain's endpoints, registered explicitly.</summary>
internal static class LogRecordsEndpoints
{
    internal static IServiceCollection AddLogRecordsEndpoints(this IServiceCollection services) =>
        services.AddSingleton<IEndpoint, CreateLogRecordsEndpoint>();
}
