using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Suggestions.Dismiss;

namespace Api.Endpoints.Suggestions.Dismiss.V1;

/// <summary>Hides a recipe from your own suggestions.</summary>
/// <remarks><c>PUT</c> and <c>DELETE</c> on the dismissal resource itself, so both are idempotent and a double tap or retry is harmless.</remarks>
internal sealed class DismissSuggestionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(
                $"{ApiPaths.V1}/recipes/{{recipeId:guid}}/suggestion-dismissal",
                async (
                    Guid recipeId,
                    HttpContext context,
                    ICommandHandler<DismissSuggestionCommand> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler
                        .Handle(
                            new DismissSuggestionCommand(
                                recipeId,
                                context.CurrentUser().UserId,
                                Hidden: true),
                            cancellationToken)
                        .ConfigureAwait(false);

                    return result.Match(Results.NoContent, CustomResults.Problem);
                })
            .WithName("dismissSuggestionV1")
            .WithTags(Tags.Suggestions)
            .WithSummary("Stop suggesting a recipe to me")
            .WithDescription(
                "Person-owned, like a note or the cook log. Hiding a recipe from your suggestions "
                + "says nothing about anybody else in the household and does not touch the recipe "
                + "itself, which stays in the collection and in every search.\n\n"
                + "It expires, so \"not tonight\" does not quietly become \"never again\". This is "
                + "the only signal the ranking cannot derive from something another feature already "
                + "records — with a household this size there is no such thing as a meaningful "
                + "non-click, so the one negative signal has to be asked for.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}

/// <summary>Takes a dismissal back.</summary>
internal sealed class RestoreSuggestionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete(
                $"{ApiPaths.V1}/recipes/{{recipeId:guid}}/suggestion-dismissal",
                async (
                    Guid recipeId,
                    HttpContext context,
                    ICommandHandler<DismissSuggestionCommand> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler
                        .Handle(
                            new DismissSuggestionCommand(
                                recipeId,
                                context.CurrentUser().UserId,
                                Hidden: false),
                            cancellationToken)
                        .ConfigureAwait(false);

                    return result.Match(Results.NoContent, CustomResults.Problem);
                })
            .WithName("restoreSuggestionV1")
            .WithTags(Tags.Suggestions)
            .WithSummary("Suggest a recipe to me again")
            .WithDescription(
                "The undo behind the toast, so it is as forgiving as the dismissal: `204`, and "
                + "`204` again when the recipe was never hidden.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
