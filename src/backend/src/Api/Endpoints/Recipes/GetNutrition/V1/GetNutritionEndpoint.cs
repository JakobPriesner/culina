using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetNutrition;
using Contracts.Recipes.GetNutrition;
using Domain.Shared;

namespace Api.Endpoints.Recipes.GetNutrition.V1;

/// <summary>Reads a recipe's nutrition.</summary>
internal sealed class GetNutritionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/nutrition", async (
                Guid recipeId,
                HttpContext context,
                IQueryHandler<GetNutritionQuery, RecipeNutrition> handler,
                CancellationToken cancellationToken) =>
            {
                var request = context.Request.Query.ReadGuid("householdId").Map(household =>
                    new GetNutritionQuery(recipeId, context.CurrentUser().UserId, household.Value));

                return await request.Match(
                    async asked =>
                    {
                        var result = await handler.Handle(asked, cancellationToken).ConfigureAwait(false);

                        return result.Match(
                            nutrition => ETag.Ok(
                                context,
                                nutrition.Body,
                                nutrition.RecipeVersion,
                                nutrition.HouseholdId,
                                $"n{nutrition.DataVersion}-{ETag.Fingerprint(nutrition.Facts)}"),
                            CustomResults.Problem);
                    },
                    error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
            })
            .WithName("getRecipeNutritionV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Read a recipe's nutrition")
            .WithDescription(
                "Per portion (or per piece, when the recipe makes pieces), worked out on every read from "
                + "the recipe's ingredient lines and the Bundeslebensmittelschlüssel; nothing is stored. "
                + "Each of the eight values carries atLeast: true when a line could not be counted or a "
                + "counted food lacks that value, so the figure is a true lower bound, never a guess. "
                + "Numbers are **unrounded**; rounding is the client's business, lower bounds downward. "
                + "A separate resource, not a field of the recipe: the recipe's ETag is its version "
                + "alone, so a household's correction or a data update would hide behind a 304 there. "
                + "A count, a spoon of a solid or a household unit is counted by the household's own "
                + "weight (via householdWeight) or, unless the household turned that off, a typical "
                + "weight (via typicalWeight, with its source): an estimate, so the value says "
                + "estimated: true. Each line with a food lists its variants, the food's usual "
                + "alternatives, to choose one by a food correction. "
                + "This tag also covers the data version, the corrections, the weights and the "
                + "typical-weights switch that apply. householdId "
                + "names whose corrections apply, for a recipe that household inherits; left out, the "
                + "recipe's own household. The source block must be shown wherever the numbers are "
                + "(CC BY 4.0).")
            .WithQueryParameters("householdId")
            .Produces<Response>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
