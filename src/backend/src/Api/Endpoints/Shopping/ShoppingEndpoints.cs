using Api.Endpoints.Shopping.AddItem.V1;
using Api.Endpoints.Shopping.AddRecipe.V1;
using Api.Endpoints.Shopping.Get.V1;
using Api.Endpoints.Shopping.RemoveItems.V1;
using Api.Endpoints.Shopping.UpdateItem.V1;

namespace Api.Endpoints.Shopping;

/// <summary>The shopping-list endpoints, registered explicitly.</summary>
internal static class ShoppingEndpoints
{
    internal static IServiceCollection AddShoppingEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetShoppingListEndpoint>()
            .AddSingleton<IEndpoint, AddShoppingItemEndpoint>()
            .AddSingleton<IEndpoint, AddRecipeToListEndpoint>()
            .AddSingleton<IEndpoint, UpdateShoppingItemEndpoint>()
            .AddSingleton<IEndpoint, RemoveShoppingItemsEndpoint>();
}
