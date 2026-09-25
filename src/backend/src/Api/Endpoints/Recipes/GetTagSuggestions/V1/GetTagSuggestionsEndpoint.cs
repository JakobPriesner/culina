using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetTagSuggestions;
using Response = Contracts.Recipes.GetTagSuggestions.Response;

namespace Api.Endpoints.Recipes.GetTagSuggestions.V1;

/// <summary>Says which tags a recipe could carry and does not.</summary>
internal sealed class GetTagSuggestionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/tag-suggestions", async (
                Guid recipeId,
                HttpContext context,
                IQueryHandler<GetTagSuggestionsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetTagSuggestionsQuery(recipeId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getRecipeTagSuggestionsV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Tags this recipe could carry")
            .WithDescription(
                "Up to five tags for what the recipe is — its dish, cuisine, meal, method or diet — "
                + "read from the recipe as it was last saved, leaving out whatever its tags already "
                + "say. Where the household already uses a tag for one of them, that tag is offered, "
                + "with its slug; otherwise the lexicon's word in the recipe's language, with none. "
                + "Offered, never applied: nothing is tagged until somebody saves the recipe with it.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
