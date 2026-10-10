using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Sources;
using Contracts.Recipes.Sources;
using Domain.Shared;

namespace Api.Endpoints.RecipeSources.GetAll.V1;

/// <summary>Lists the libraries a household has connected.</summary>
internal sealed class GetRecipeSourcesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipe-sources", async (
                HttpContext context,
                IQueryHandler<GetSourcesQuery, SourcesResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var request = context.Request.Query.RequireGuid("householdId").Map(household =>
                    new GetSourcesQuery(household, context.CurrentUser().UserId));

                return await request.Match(
                    async asked =>
                    {
                        var result = await handler.Handle(asked, cancellationToken).ConfigureAwait(false);

                        return result.Match(Results.Ok, CustomResults.Problem);
                    },
                    error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
            })
            .WithName("getRecipeSourcesV1")
            .WithTags(Tags.RecipeSources)
            .WithSummary("List a household's connected recipe libraries")
            .WithDescription(
                "Not paginated: a kitchen connects one or two other apps, not a hundred. Each "
                + "carries when it was connected and when recipes were last brought over, which "
                + "is the difference between a connection worth offering again and one somebody "
                + "set up and forgot. Tokens are never included.")
            .WithRepeatableQueryParameters(["householdId"], [])
            .Produces<SourcesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
