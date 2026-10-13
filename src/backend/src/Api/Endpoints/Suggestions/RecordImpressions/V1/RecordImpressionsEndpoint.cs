using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Suggestions.RecordImpressions;
using Request = Contracts.Suggestions.RecordImpressions.Request;

namespace Api.Endpoints.Suggestions.RecordImpressions.V1;

/// <summary>Says which recipes a suggestion set showed you.</summary>
internal sealed class RecordImpressionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/suggestions/impressions", async (
                Request request,
                HttpContext context,
                ICommandHandler<RecordImpressionsCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new RecordImpressionsCommand(
                            request.HouseholdId,
                            context.CurrentUser().UserId,
                            request.RecipeIds),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("recordSuggestionImpressionsV1")
            .WithTags(Tags.Suggestions)
            .WithSummary("Say which suggestions you were shown")
            .WithDescription(
                "Sent once the set has actually been drawn, never as a side effect of `GET /suggestions`, "
                + "which stays a read: a response that never painted was never shown.\n\n"
                + "Person-owned. What was shown on earlier days is pushed down in the next shortlists, "
                + "so the same five recipes are not offered all week. The day is the server's, and "
                + "today's own impressions never change today's list. Recording the same recipe again "
                + "is harmless, so a refresh or a second device writes nothing new. Recipes outside "
                + "the household's library are ignored.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
