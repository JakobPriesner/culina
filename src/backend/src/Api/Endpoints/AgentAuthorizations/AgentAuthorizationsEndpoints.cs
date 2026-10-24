using Api.Endpoints.AgentAuthorizations.Decide.V1;
using Api.Endpoints.AgentAuthorizations.GetById.V1;

namespace Api.Endpoints.AgentAuthorizations;

/// <summary>The consent screen's endpoints for apps connecting AI agents through OAuth, registered explicitly.</summary>
internal static class AgentAuthorizationsEndpoints
{
    internal static IServiceCollection AddAgentAuthorizationsEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetAgentAuthorizationByIdEndpoint>()
            .AddSingleton<IEndpoint, DecideAgentAuthorizationEndpoint>();
}
