using Api.Endpoints.Suggestions.Dismiss.V1;
using Api.Endpoints.Suggestions.GetAll.V1;

namespace Api.Endpoints.Suggestions;

/// <summary>The suggestions domain's endpoints, registered explicitly.</summary>
internal static class SuggestionsEndpoints
{
    internal static IServiceCollection AddSuggestionsEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, GetSuggestionsEndpoint>()
            // Hang off a recipe rather than off /suggestions, because "do not
            // suggest this one to me" is a fact about that recipe and this
            // person — the same shape as its notes and its cook log.
            .AddSingleton<IEndpoint, DismissSuggestionEndpoint>()
            .AddSingleton<IEndpoint, RestoreSuggestionEndpoint>();
}
