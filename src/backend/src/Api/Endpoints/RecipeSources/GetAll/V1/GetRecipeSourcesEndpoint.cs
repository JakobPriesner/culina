using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Sources;
using Contracts.Recipes.Sources;

namespace Api.Endpoints.RecipeSources.GetAll.V1;

/// <summary>Lists the libraries a household has connected.</summary>
internal sealed class GetRecipeSourcesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipe-sources", async (
                Guid? householdId,
                HttpContext context,
                IQueryHandler<GetSourcesQuery, SourcesResponse> handler,
                CancellationToken cancellationToken) =>
            {
                if (householdId is not { } household)
                {
                    return CustomResults.Problem(RequestErrors.MissingQueryParameter("householdId"));
                }

                var result = await handler
                    .Handle(
                        new GetSourcesQuery(household, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
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
