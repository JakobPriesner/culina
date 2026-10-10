using Api.Endpoints.Nutrition.GetFoods.V1;
using Api.Endpoints.Nutrition.RemoveFood.V1;
using Api.Endpoints.Nutrition.SetFood.V1;

namespace Api.Endpoints.Nutrition;

/// <summary>The nutrition endpoints beyond a recipe's own figure, registered explicitly.</summary>
internal static class NutritionEndpoints
{
    internal static IServiceCollection AddNutritionEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetFoodsEndpoint>()
            .AddSingleton<IEndpoint, SetFoodEndpoint>()
            .AddSingleton<IEndpoint, RemoveFoodEndpoint>();
}
