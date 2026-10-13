using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Cookbooks;
using Contracts.Cookbooks;

namespace Api.Endpoints.Cookbooks.Reorder.V1;

/// <summary>Arranges the recipes on a chosen cookbook.</summary>
internal sealed class ReorderCookbookRecipesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/cookbooks/{{cookbookId:guid}}/recipes", async (
                Guid cookbookId,
                ReorderCookbookRecipesRequest request,
                HttpContext context,
                ICommandHandler<ReorderCookbookRecipesCommand, CookbookDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var expected = ETag.RequireIfMatch(context);

                return await expected.Match(
                    async version =>
                    {
                        var result = await handler
                            .Handle(
                                new ReorderCookbookRecipesCommand(
                                    cookbookId,
                                    context.CurrentUser().UserId,
                                    version,
                                    request.RecipeIds),
                                cancellationToken)
                            .ConfigureAwait(false);

                        return result.Match(
                            cookbook => CookbookTag.Ok(context, cookbook),
                            CustomResults.Problem);
                    },
                    error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
            })
            .WithName("reorderCookbookRecipesV1")
            .WithTags(Tags.Cookbooks)
            .WithSummary("Arrange a cookbook's recipes")
            .WithDescription(
                "Replaces the order of a cookbook filled by hand: `recipeIds` lists every recipe on it, "
                + "each once, first first. Anything else is 400 `cookbooks.order_mismatch`, never a guess "
                + "at where a missing recipe should go. A recipe added later goes last. A smart cookbook "
                + "has no order to choose (409 `cookbooks.rules_decide_membership`). The order is part of "
                + "the cookbook, so `If-Match` is required — missing is 428, stale is 412 — and the "
                + "answer is the cookbook with its new version and cover.")
            .Produces<CookbookDetail>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization();
    }
}
