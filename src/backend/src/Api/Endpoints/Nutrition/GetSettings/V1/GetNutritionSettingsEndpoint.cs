using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Nutrition;
using Contracts.Nutrition;

namespace Api.Endpoints.Nutrition.GetSettings.V1;

/// <summary>Reads how a household's nutrition figures are worked out.</summary>
internal sealed class GetNutritionSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/nutrition", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetNutritionSettingsQuery, NutritionSettings> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetNutritionSettingsQuery(householdId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getHouseholdNutritionV1")
            .WithTags(Tags.Nutrition)
            .WithSummary("Read how a household's nutrition is worked out")
            .WithDescription(
                "`useTypicalWeights` is true until the household turns typical weights off. Any member.")
            .Produces<NutritionSettings>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
