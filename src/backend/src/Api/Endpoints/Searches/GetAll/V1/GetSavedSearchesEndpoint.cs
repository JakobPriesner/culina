using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Searches;
using Contracts.Searches;
using Domain.Shared;

namespace Api.Endpoints.Searches.GetAll.V1;

/// <summary>Every search a household has saved.</summary>
internal sealed class GetSavedSearchesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/searches", async (
                HttpContext context,
                IQueryHandler<GetSavedSearchesQuery, SavedSearchesResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var request = context.Request.Query.RequireGuid("householdId").Map(household =>
                    new GetSavedSearchesQuery(household, context.CurrentUser().UserId));

                return await request.Match(
                    async asked =>
                    {
                        var result = await handler.Handle(asked, cancellationToken).ConfigureAwait(false);

                        return result.Match(Results.Ok, CustomResults.Problem);
                    },
                    error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
            })
            .WithName("getSavedSearchesV1")
            .WithTags(Tags.Searches)
            .WithSummary("The searches this household has saved")
            .WithDescription(
                "Oldest first, so the row of chips beside the search field keeps the order they "
                + "were made in. Not paged: a kitchen keeps a handful of these and they are drawn "
                + "on one line.\n\n"
                + "Each carries the four values `GET /recipes` takes as `query`, `tag`, "
                + "`maxMinutes` and `sort`, so applying one is assigning them rather than "
                + "translating anything.")
            .WithQueryParameters("householdId")
            .Produces<SavedSearchesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
