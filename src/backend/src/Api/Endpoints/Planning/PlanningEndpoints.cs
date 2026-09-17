using Api.Endpoints.Planning.MealPlan.V1;

namespace Api.Endpoints.Planning;

/// <summary>The planning domain's endpoints, registered explicitly.</summary>
internal static class PlanningEndpoints
{
    internal static IServiceCollection AddPlanningEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetMealPlanEndpoint>()
            .AddSingleton<IEndpoint, PlanMealEndpoint>()
            .AddSingleton<IEndpoint, UnplanMealEndpoint>()
            .AddSingleton<IEndpoint, MoveMealEndpoint>();
}
