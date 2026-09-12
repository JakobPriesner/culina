using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.SaveNotes;
using Request = Contracts.Recipes.SaveNotes.Request;
using Response = Contracts.Recipes.GetNotes.Response;

namespace Api.Endpoints.Recipes.SaveNotes.V1;

/// <summary>Replaces your notes on a recipe.</summary>
internal sealed class SaveNotesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/notes", async (
                Guid recipeId,
                Request request,
                HttpContext context,
                ICommandHandler<SaveNotesCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(request.ToCommand(recipeId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("saveRecipeNotesV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Write your notes")
            .WithDescription(
                "A replacement. An omitted or empty note is deleted rather than stored blank, so "
                + "the panel never shows a box nobody asked for.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
