using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetNotes;
using Response = Contracts.Recipes.GetNotes.Response;

namespace Api.Endpoints.Recipes.GetNotes.V1;

/// <summary>Reads your notes on a recipe.</summary>
internal sealed class GetNotesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/notes", async (
                Guid recipeId,
                HttpContext context,
                IQueryHandler<GetNotesQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetNotesQuery(recipeId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getRecipeNotesV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Read your notes")
            .WithDescription(
                "Your own notes only. Another member of the same household never sees them: the "
                + "recipe is shared and canonical, the note is yours.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
