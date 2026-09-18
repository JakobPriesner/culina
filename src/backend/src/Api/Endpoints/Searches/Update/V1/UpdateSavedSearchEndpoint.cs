using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Searches;
using Contracts.Searches;

namespace Api.Endpoints.Searches.Update.V1;

/// <summary>Renames a saved search, and rewrites what it asks for.</summary>
internal sealed class UpdateSavedSearchEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPatch($"{ApiPaths.V1}/searches/{{searchId:guid}}", async (
                Guid searchId,
                UpdateSavedSearchRequest request,
                HttpContext context,
                ICommandHandler<UpdateSavedSearchCommand, SavedSearchDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new UpdateSavedSearchCommand(
                            searchId,
                            context.CurrentUser().UserId,
                            request),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("updateSavedSearchV1")
            .WithTags(Tags.Searches)
            .WithSummary("Rename a saved search, or point it at different filters")
            .WithDescription(
                "The name and the filters together: renaming a search and pointing it at what you "
                + "are looking at now are the same gesture from the same sheet, and two requests "
                + "for one gesture is two ways for half of it to fail.\n\n"
                + "No `If-Match`, unlike a recipe or a cookbook. The one edit anybody makes is "
                + "\"save what I am looking at now over what I saved before\", which is a "
                + "deliberate overwrite rather than a clash — a precondition would exist only so "
                + "that the answer to it could be to overwrite anyway.")
            .Produces<SavedSearchDetail>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();
    }
}
