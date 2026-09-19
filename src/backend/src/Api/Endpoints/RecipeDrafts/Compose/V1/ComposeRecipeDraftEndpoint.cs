using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Drafts;
using Request = Contracts.Recipes.Drafts.Request;
using Response = Contracts.Recipes.Drafts.Response;

namespace Api.Endpoints.RecipeDrafts.Compose.V1;

/// <summary>Asks the assistant for a recipe.</summary>
internal sealed class ComposeRecipeDraftEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipe-drafts", async (
                Request request,
                HttpContext context,
                ICommandHandler<ComposeRecipeDraftCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new ComposeRecipeDraftCommand(
                            request.Kind,
                            request.HouseholdId,
                            request.Material,
                            request.RecipeId,
                            request.Language,
                            context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("composeRecipeDraftV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Ask the assistant for a recipe")
            .WithDescription(
                "A draft, never a recipe. Nothing is created: what comes back is shown for "
                + "correction and accepted a field at a time through the ordinary recipe "
                + "endpoints.\n\n"
                + "`kind` says which of three: `idea` turns a sentence about dinner into a "
                + "draft, `text` reads one out of something pasted, and `revision` rewrites "
                + "the recipe named by `recipeId` — keeping its ingredients, its amounts and "
                + "its language, and changing only how it reads.\n\n"
                + "404 when this instance has no assistant, or has that capability switched "
                + "off; the two are one answer because a caller learns nothing from being "
                + "told which. 429 when the month's budget is spent.\n\n"
                + "The answer is checked on the way out: a line the app could not store loses "
                + "the part it could not store rather than failing the whole draft, because a "
                + "draft is a thing somebody is about to correct anyway.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting(RateLimitExtensions.Assistance)
            .RequireAuthorization();
    }
}
