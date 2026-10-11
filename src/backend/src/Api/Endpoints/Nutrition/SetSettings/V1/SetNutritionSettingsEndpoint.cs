using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Nutrition;
using Contracts.Nutrition;

namespace Api.Endpoints.Nutrition.SetSettings.V1;

/// <summary>Chooses whether typical weights count lines for a household.</summary>
internal sealed class SetNutritionSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/households/{{householdId:guid}}/nutrition", async (
                Guid householdId,
                NutritionSettings request,
                HttpContext context,
                ICommandHandler<SetNutritionSettingsCommand, NutritionSettings> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new SetNutritionSettingsCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            request.UseTypicalWeights),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("setHouseholdNutritionV1")
            .WithTags(Tags.Nutrition)
            .WithSummary("Choose whether typical weights count")
            .WithDescription(
                "`{ \"useTypicalWeights\": false }` makes this household's nutrition count only what an "
                + "amount, a density, an egg size or the household's own weight can: a count of onions "
                + "is then not counted unless the household set a weight. It applies to every recipe of "
                + "this household and never to another household. Any member may. Idempotent; 200 with "
                + "the new state.")
            .Produces<NutritionSettings>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
