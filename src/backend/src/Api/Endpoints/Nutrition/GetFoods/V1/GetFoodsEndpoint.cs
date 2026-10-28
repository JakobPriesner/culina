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

                        // Reference data: the answer changes only with the shipped table and how it is
                        // searched, and a cache keys on the URL, so the data version is the whole tag.
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
                "Foods of the Bundeslebensmittelschlüssel whose German or English name holds every word "
                + "of `q`, in any order: a word of the name that starts with it, else one that contains "
                + "it (three letters or more), else one a small typo away (one edit from five letters, "
                + "two from nine). Two words of a name also count joined, so \"Haferflocken\" finds "
                + "\"Hafer Flocken\". Umlauts may be typed as ä, ae or a; punctuation is ignored. Best "
                + "first: by the worst-fitting word, then names whose first word fits the first word "
                + "of `q`, then shorter names. Not household data, so any signed-in person may ask. "
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
