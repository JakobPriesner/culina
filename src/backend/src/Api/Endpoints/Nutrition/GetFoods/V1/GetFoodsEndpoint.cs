using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Nutrition;
using Contracts.Nutrition;
using Domain.Nutrition;
using Domain.Shared;

namespace Api.Endpoints.Nutrition.GetFoods.V1;

/// <summary>Searches the foods of the Bundeslebensmittelschlüssel.</summary>
internal sealed class GetFoodsEndpoint : IEndpoint
{
    private const int DefaultLimit = 20;

    private const int MaxLimit = 50;

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/foods", async (
                HttpContext context,
                IQueryHandler<GetFoodsQuery, FoodsResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var query = context.Request.Query;

                var request = query.ReadInt("limit", 1, MaxLimit, DefaultLimit)
                    .Map(limit => new GetFoodsQuery(query["q"], limit));

                return await request.Match(
                    async asked =>
                    {
                        var result = await handler.Handle(asked, cancellationToken).ConfigureAwait(false);

                        // Reference data: the answer changes only with the shipped table, and a cache keys
                        // on the URL, so the data version is the whole tag.
                        return result.Match(
                            foods => ETag.Ok(context, foods, NutritionData.Version),
                            CustomResults.Problem);
                    },
                    error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
            })
            .WithName("getFoodsV1")
            .WithTags(Tags.Nutrition)
            .WithSummary("Search foods")
            .WithDescription(
                "Foods of the Bundeslebensmittelschlüssel whose German or English name fits `q`, "
                + "best first: names that start with it, then names with a word that starts with it, "
                + "then names that contain it. Not household data, so any signed-in person may ask. "
                + "No paging: at most `limit` (default 20, at most 50). energyKcal is per 100 g and "
                + "tells apart the many foods of one name.")
            .WithRepeatableQueryParameters(["q", "limit"], [], ["limit"])
            .Produces<FoodsResponse>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
