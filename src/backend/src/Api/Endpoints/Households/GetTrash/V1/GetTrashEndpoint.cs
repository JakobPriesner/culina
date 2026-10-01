using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.GetTrash;
using Response = Contracts.Households.GetTrash.Response;

namespace Api.Endpoints.Households.GetTrash.V1;

/// <summary>Lists what is in a household's bin.</summary>
internal sealed class GetTrashEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/trash", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetTrashQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetTrashQuery(householdId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getTrashV1")
            .WithTags(Tags.Households)
            .WithSummary("List a household's bin")
            .WithDescription(
                "The recipes and cookbooks deleted in this household that can still be restored, "
                + "newest first, each with when it will be purged. Any member may look.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
