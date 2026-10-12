using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Ingredients;
using Contracts.Ingredients;

namespace Api.Endpoints.Ingredients.GetMergeSuggestions.V1;

/// <summary>Proposes merges for ingredient names that look like one ingredient.</summary>
internal sealed class GetMergeSuggestionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/ingredient-merge-suggestions", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetMergeSuggestionsQuery, MergeSuggestionsResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetMergeSuggestionsQuery(householdId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getIngredientMergeSuggestionsV1")
            .WithTags(Tags.Ingredients)
            .WithSummary("Suggest ingredient merges")
            .WithDescription(
                "Names in this household's own recipes that are probably one ingredient: spelt the "
                + "same once folded (`spelling`), naming the same food (`same_food`, e.g. "
                + "\"Hühnchenbrustfilet\" and \"Hähnchenbrust\") or a letter or two apart (`typo`). Each "
                + "is proposed to go into the most used of its lookalikes. Names the household kept "
                + "apart are never proposed. Accepting is `POST ingredient-merges`.")
            .Produces<MergeSuggestionsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
