using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Suggestions.GetAll;
using Response = Contracts.Suggestions.GetAll.Response;

namespace Api.Endpoints.Suggestions.GetAll.V1;

/// <summary>Suggests a handful of recipes for one occasion.</summary>
internal sealed class GetSuggestionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/suggestions", async (
                HttpContext context,
                TimeProvider time,
                IQueryHandler<GetSuggestionsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                // The day, not the instant. Every decayed term is a function of
                // it, so two requests on one day answer identically: the list is
                // the same all evening, on both devices and after a refresh, and
                // different tomorrow. That is why there is no refresh control —
                // one would teach people the first answer was arbitrary.
                var now = time.GetUtcNow();
                var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);

                var occasion = context.Request.Query
                    .ToSuggestionContext(context.CurrentUser().UserId, today);

                return await occasion.Match(
                    async criteria =>
                    {
                        var result = await handler
                            .Handle(new GetSuggestionsQuery(criteria), cancellationToken)
                            .ConfigureAwait(false);

                        return result.Match(Results.Ok, CustomResults.Problem);
                    },
                    error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
            })
            .WithName("getSuggestionsV1")
            .WithTags(Tags.Suggestions)
            .WithSummary("Suggest recipes for an occasion")
            .WithDescription(
                "A small, bounded set with a reason for each, best first. Deliberately not paged: "
                + "ranking the whole collection is `GET /recipes?sort=suggested`, which pages and "
                + "composes with every other filter.\n\n"
                + "Context is supplied here rather than stored on a recipe, because whether "
                + "something is breakfast is a fact about the occasion and about how this household "
                + "plans, not a property of the food. `slot`, `maxMinutes`, `tag` and `ingredient` "
                + "are the caller's constraints and are honoured exactly; `exclude` is what the "
                + "caller already has on screen.\n\n"
                + "`likeRecipeId` asks for recipes resembling one, by shared ingredients and tags "
                + "rather than by who else cooked them — with this many people, co-occurrence "
                + "between two recipes is noise, and 'uses eleven of the same twelve ingredients' "
                + "is not.\n\n"
                + "`reason` is null whenever no single signal decided the ranking. That is an "
                + "ordinary answer and means show nothing: an invented explanation discredits the "
                + "ones that were true.")
            .WithRepeatableQueryParameters(
                ["householdId", "purpose", "slot", "maxMinutes", "likeRecipeId", "limit"],
                ["tag", "ingredient", "exclude"],
                ["maxMinutes", "limit"])
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
