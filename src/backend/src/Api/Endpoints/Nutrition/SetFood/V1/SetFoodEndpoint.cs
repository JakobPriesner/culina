using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Nutrition;
using Request = Contracts.Nutrition.SetFoodRequest;

namespace Api.Endpoints.Nutrition.SetFood.V1;

/// <summary>Says what a household means by an ingredient name.</summary>
internal sealed class SetFoodEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/households/{{householdId:guid}}/ingredients/{{name}}", async (
                Guid householdId,
                string name,
                Request request,
                HttpContext context,
                ICommandHandler<SetFoodCorrectionCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new SetFoodCorrectionCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            PathSegments.LastDecoded(context, name),
                            request.Food),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("setHouseholdIngredientFoodV1")
            .WithTags(Tags.Nutrition)
            .WithSummary("Say what an ingredient is, for nutrition")
            .WithDescription(
                "States what this household means by an ingredient name: a BLS code from "
                + "`GET /foods`, or null for \"do not count this\". It applies to every recipe of "
                + "this household that uses the name, spelled any way that folds to the same key "
                + "(\"Müsli\" and \"Muesli\"), and never to another household, including the one "
                + "this household inherits from. `{name}` is the ingredient as written, "
                + "percent-encoded; a slash is `%2F`. Any member may correct. Idempotent; 204.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
