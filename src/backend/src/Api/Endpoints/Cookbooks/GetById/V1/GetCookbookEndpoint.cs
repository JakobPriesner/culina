using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Cookbooks;
using Contracts.Cookbooks;

namespace Api.Endpoints.Cookbooks.GetById.V1;

/// <summary>Reads one cookbook.</summary>
internal sealed class GetCookbookEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/cookbooks/{{cookbookId:guid}}", async (
                Guid cookbookId,
                HttpContext context,
                IQueryHandler<GetCookbookQuery, CookbookDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new GetCookbookQuery(cookbookId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    cookbook => ETag.Ok(context, cookbook, cookbook.Version),
                    CustomResults.Problem);
            })
            .WithName("getCookbookByIdV1")
            .WithTags(Tags.Cookbooks)
            .WithSummary("Read a cookbook")
            .WithDescription(
                "The shelf itself: its name, what it is for, how many recipes are on it and the "
                + "pictures its cover shows. Not the recipes — those are `GET /recipes?cookbookId=…`, "
                + "so one screen's worth arrives at a time and every filter still works.")
            .Produces<CookbookDetail>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
