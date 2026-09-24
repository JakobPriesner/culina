using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetCompletions;
using Response = Contracts.Recipes.GetCompletions.Response;

namespace Api.Endpoints.Recipes.GetCompletions.V1;

/// <summary>Completes a half-typed search.</summary>
/// <remarks>
/// A household sub-resource, beside <c>/households/{householdId}/ingredients</c>,
/// and deliberately not <c>/suggestions</c>: that is the "what should I cook?"
/// ranking, and a completion is an answer to what somebody is typing, not to
/// what they might like.
/// </remarks>
internal sealed class GetCompletionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/completions", async (
                Guid householdId,
                string? query,
                HttpContext context,
                IQueryHandler<GetCompletionsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetCompletionsQuery(householdId, context.CurrentUser().UserId, query), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getHouseholdCompletionsV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Complete a search")
            .WithDescription(
                "What the words still being typed could become, from this household's own recipes: "
                + "recipes to open, ingredients and tags to filter by, and at most one refinement. "
                + "Whatever `query` already says about a diet, time or meal is left out of the "
                + "completion. Never cached.")
            .WithQueryParameters("query")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
