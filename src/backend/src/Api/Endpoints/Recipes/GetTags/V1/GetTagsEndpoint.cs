using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetTags;
using Response = Contracts.Recipes.GetTags.Response;

namespace Api.Endpoints.Recipes.GetTags.V1;

/// <summary>The tags a household's recipes carry.</summary>
internal sealed class GetTagsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/tags", async (
                Guid? householdId,
                HttpContext context,
                IQueryHandler<GetTagsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                if (householdId is not { } household)
                {
                    return CustomResults.Problem(RequestErrors.MissingQueryParameter("householdId"));
                }

                var result = await handler
                    .Handle(
                        new GetTagsQuery(household, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getTagsV1")
            .WithTags(Tags.Recipes)
            .WithSummary("The tags this household uses")
            .WithDescription(
                "With usage counts, most used first. Not paged: a household's vocabulary is a few "
                + "dozen words, and there is no tag management — a tag exists because a recipe "
                + "carries it and stops existing when the last one lets it go.")
            .WithQueryParameters("householdId")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
