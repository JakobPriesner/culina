using Api.Endpoints.Searches.Create.V1;
using Api.Endpoints.Searches.Delete.V1;
using Api.Endpoints.Searches.GetAll.V1;
using Api.Endpoints.Searches.Update.V1;

namespace Api.Endpoints.Searches;

/// <summary>The saved searches domain's endpoints, registered explicitly.</summary>
internal static class SearchesEndpoints
{
    internal static IServiceCollection AddSearchesEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetSavedSearchesEndpoint>()
            .AddSingleton<IEndpoint, CreateSavedSearchEndpoint>()
            .AddSingleton<IEndpoint, UpdateSavedSearchEndpoint>()
            .AddSingleton<IEndpoint, DeleteSavedSearchEndpoint>();
}
