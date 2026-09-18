using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Searches;
using Contracts.Searches;

namespace Api.Endpoints.Searches.Create.V1;

/// <summary>Saves a search.</summary>
internal sealed class CreateSavedSearchEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/searches", async (
                CreateSavedSearchRequest request,
                HttpContext context,
                ICommandHandler<CreateSavedSearchCommand, SavedSearchDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new CreateSavedSearchCommand(context.CurrentUser().UserId, request),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    saved => Results.Created($"{ApiPaths.V1}/searches/{saved.SearchId}", saved),
                    CustomResults.Problem);
            })
            .WithName("createSavedSearchV1")
            .WithTags(Tags.Searches)
            .WithSummary("Save a search")
            .WithDescription(
                "A name over the filters the library is showing. At least one of `query`, `tags`, "
                + "`maxMinutes` and `sort` must be set — a search asking for nothing is the "
                + "library, which is the screen it would be applied from.\n\n"
                + "Names are unique within a household, because two chips with one label are two "
                + "things nobody can tell apart.")
            .Produces<SavedSearchDetail>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();
    }
}
