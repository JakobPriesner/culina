using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetIngredients;
using Response = Contracts.Recipes.GetIngredients.Response;

namespace Api.Endpoints.Recipes.GetIngredients.V1;

/// <summary>Suggests what an ingredient line could be about.</summary>
internal sealed class GetIngredientsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/ingredients", async (
                Guid householdId,
                string? q,
                string? language,
                HttpContext context,
                IQueryHandler<GetIngredientsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var query = new GetIngredientsQuery(
                    householdId,
                    context.CurrentUser().UserId,
                    q,
                    language);

                var result = await handler.Handle(query, cancellationToken).ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getHouseholdIngredientsV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Suggest an ingredient")
            .WithDescription(
                "What this household has written before, then a short seeded list of what a home "
                + "kitchen buys. Suggestions only: an ingredient is whatever somebody types, and "
                + "nothing has to be chosen from either list.")
            .WithQueryParameters("q", "language")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
