using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Cookbooks;
using Contracts.Cookbooks;

namespace Api.Endpoints.Cookbooks.GetAll.V1;

/// <summary>Lists a household's cookbooks.</summary>
internal sealed class GetCookbooksEndpoint : IEndpoint
{
    /// <summary>Enough for a shelf, and the same default the recipe grid uses.</summary>
    private const int DefaultLimit = 24;

    private const int MaxLimit = 100;

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/cookbooks", async (
                Guid? householdId,
                string? cursor,
                int? limit,
                HttpContext context,
                IQueryHandler<GetCookbooksQuery, CookbooksResponse> handler,
                CancellationToken cancellationToken) =>
            {
                if (householdId is not { } household)
                {
                    return CustomResults.Problem(RequestErrors.MissingQueryParameter("householdId"));
                }

                var result = await handler
                    .Handle(
                        new GetCookbooksQuery(
                            household,
                            context.CurrentUser().UserId,
                            cursor,
                            Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getCookbooksV1")
            .WithTags(Tags.Cookbooks)
            .WithSummary("List a household's cookbooks")
            .WithDescription(
                "Cursor-paginated, most recently changed first. Each carries its recipe count and "
                + "up to four pictures for its cover; the recipes themselves are read through "
                + "`GET /recipes?cookbookId=…`, which is what gives a cookbook the same search, "
                + "filters and paging the whole collection has.")
            .WithRepeatableQueryParameters(["householdId", "cursor", "limit"], [], ["limit"])
            .Produces<CookbooksResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
