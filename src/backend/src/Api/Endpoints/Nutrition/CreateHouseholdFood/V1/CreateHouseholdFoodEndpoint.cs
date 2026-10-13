using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Nutrition;
using Contracts.Nutrition;
using Domain.Nutrition;
using Request = Contracts.Nutrition.CreateHouseholdFoodRequest;

namespace Api.Endpoints.Nutrition.CreateHouseholdFood.V1;

/// <summary>Keeps a food typed from a package's label.</summary>
internal sealed class CreateHouseholdFoodEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/households/{{householdId:guid}}/foods", async (
                Guid householdId,
                Request request,
                HttpContext context,
                ICommandHandler<CreateHouseholdFoodCommand, HouseholdFoodDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new CreateHouseholdFoodCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            request.Name,
                            new Nutrients(
                                request.EnergyKj,
                                request.EnergyKcal,
                                request.Fat,
                                request.SaturatedFat,
                                request.Carbohydrate,
                                request.Sugars,
                                request.Protein,
                                request.Salt)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    food => Results.Created($"{ApiPaths.V1}/households/{householdId}/foods/{food.Id}", food),
                    CustomResults.Problem);
            })
            .WithName("createHouseholdFoodV1")
            .WithTags(Tags.Nutrition)
            .WithSummary("Add a food from a package")
            .WithDescription(
                "Keeps a food with the values its package's label gives per 100 g, cited as \"from the package\". "
                + "Kilocalories are required; kilojoules follow from them when left out, and any other value left "
                + "out is unknown, never zero. A food is not edited: remove it and add it again. Any member may add.")
            .Produces<HouseholdFoodDetail>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
