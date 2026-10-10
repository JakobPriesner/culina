using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Cookbooks;
using Contracts.Cookbooks;
using Domain.Shared;

namespace Api.Endpoints.Recipes.GetCookbooks.V1;

/// <summary>Says which cookbooks a recipe is on.</summary>
internal sealed class GetRecipeCookbooksEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/cookbooks", async (
                Guid recipeId,
                HttpContext context,
                IQueryHandler<GetRecipeCookbooksQuery, RecipeCookbooksResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var request = context.Request.Query.ReadGuid("householdId").Map(household =>
                    new GetRecipeCookbooksQuery(recipeId, context.CurrentUser().UserId, household.Value));

                return await request.Match(
                    async asked =>
                    {
                        var result = await handler.Handle(asked, cancellationToken).ConfigureAwait(false);

                        return result.Match(Results.Ok, CustomResults.Problem);
                    },
                    error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
            })
            .WithName("getRecipeCookbooksV1")
            .WithTags(Tags.Cookbooks)
            .WithSummary("Which cookbooks a recipe is on")
            .WithDescription(
                "Not paged. A recipe is on a handful of shelves or none, and this answers the tick "
                + "marks in the add-to-cookbook sheet and the line under a recipe's title — a "
                + "cursor would be machinery for a list that fits on one screen. householdId names "
                + "whose shelves to look on, for a recipe that household inherits; left out, the "
                + "recipe's own household.")
            .WithQueryParameters("householdId")
            .Produces<RecipeCookbooksResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
