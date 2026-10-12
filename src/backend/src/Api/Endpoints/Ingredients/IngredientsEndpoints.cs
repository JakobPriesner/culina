using Api.Endpoints.Ingredients.CreateMerge.V1;
using Api.Endpoints.Ingredients.DeleteMerge.V1;
using Api.Endpoints.Ingredients.DeleteMergeSuggestion.V1;
using Api.Endpoints.Ingredients.GetMerges.V1;
using Api.Endpoints.Ingredients.GetMergeSuggestions.V1;
using Api.Endpoints.Ingredients.GetNames.V1;

namespace Api.Endpoints.Ingredients;

/// <summary>A household's ingredient names and its merges of them, registered explicitly.</summary>
internal static class IngredientsEndpoints
{
    internal static IServiceCollection AddIngredientsEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetIngredientNamesEndpoint>()
            .AddSingleton<IEndpoint, GetIngredientMergesEndpoint>()
            .AddSingleton<IEndpoint, CreateIngredientMergeEndpoint>()
            .AddSingleton<IEndpoint, DeleteIngredientMergeEndpoint>()
            .AddSingleton<IEndpoint, GetMergeSuggestionsEndpoint>()
            .AddSingleton<IEndpoint, DeleteMergeSuggestionEndpoint>();
}
