using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Cookbooks;
using Contracts.Cookbooks;

namespace Api.Endpoints.Cookbooks.Create.V1;

/// <summary>Starts a cookbook.</summary>
internal sealed class CreateCookbookEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/cookbooks", async (
                CreateCookbookRequest request,
                HttpContext context,
                ICommandHandler<CreateCookbookCommand, CookbookDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new CreateCookbookCommand(context.CurrentUser().UserId, request),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    created => Results.Created($"{ApiPaths.V1}/cookbooks/{created.CookbookId}", created),
                    CustomResults.Problem);
            })
            .WithName("createCookbookV1")
            .WithTags(Tags.Cookbooks)
            .WithSummary("Start a cookbook")
            .WithDescription(
                "Only a household and a name. \"Christmas\" is a complete thought, and a form that "
                + "asks for more before it will save is one people abandon halfway through having "
                + "the idea.\n\n"
                + "Send `rules` to make a cookbook that fills itself: everything matching them is "
                + "on it, worked out whenever it is read, so a recipe written this evening is on "
                + "the right shelf the moment it is saved. Omit `rules` to make one you fill "
                + "yourself. A cookbook cannot be both, and cannot change which it is.")
            .Produces<CookbookDetail>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
