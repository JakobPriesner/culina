using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Ingredients;
using Contracts.Ingredients;

namespace Api.Endpoints.Ingredients.GetNouns.V1;

/// <summary>Lists the words a household writes in both numbers.</summary>
internal sealed class GetIngredientNounsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/ingredient-nouns", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetIngredientNounsQuery, IngredientNounsResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetIngredientNounsQuery(householdId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getIngredientNounsV1")
            .WithTags(Tags.Ingredients)
            .WithSummary("List a household's own singular and plural words")
            .WithDescription(
                "The ingredient words this household writes in both numbers, beyond the counted foods "
                + "Culina knows. A scaled recipe and a summed shopping line use them to say \"2 Maultaschen\" "
                + "rather than \"2 Maultasche\".")
            .Produces<IngredientNounsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
