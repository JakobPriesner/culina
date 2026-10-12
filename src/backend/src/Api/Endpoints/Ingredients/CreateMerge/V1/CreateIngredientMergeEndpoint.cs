using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Ingredients;
using Contracts.Ingredients;

namespace Api.Endpoints.Ingredients.CreateMerge.V1;

/// <summary>Merges one ingredient name into another.</summary>
internal sealed class CreateIngredientMergeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/households/{{householdId:guid}}/ingredient-merges", async (
                Guid householdId,
                CreateIngredientMergeRequest request,
                HttpContext context,
                ICommandHandler<CreateIngredientMergeCommand, CreatedIngredientMerge> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new CreateIngredientMergeCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            request.From,
                            request.To),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    created => Results.Created(
                        $"{ApiPaths.V1}/households/{householdId}/ingredient-merges/{Uri.EscapeDataString(created.From)}",
                        created),
                    CustomResults.Problem);
            })
            .WithName("createIngredientMergeV1")
            .WithTags(Tags.Ingredients)
            .WithSummary("Merge an ingredient name into another")
            .WithDescription(
                "Rewrites every line of this household's own recipes named `from` (spelled any way that "
                + "folds the same: \"Olivenoel\", \"olivenöl\") to `to`, and keeps doing so for every later "
                + "save, import, copy or restore. `to` need not be in use yet, so this is also how an "
                + "ingredient is renamed. Merging into a name that was itself merged follows that merge; "
                + "merges that kept `from` now keep `to`. A nutrition or shopping choice made for `from` "
                + "moves to `to` unless `to` has its own. Each rewritten recipe gets a new version. "
                + "Merging again from the same name replaces the merge. Any member may merge.")
            .Produces<CreatedIngredientMerge>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
