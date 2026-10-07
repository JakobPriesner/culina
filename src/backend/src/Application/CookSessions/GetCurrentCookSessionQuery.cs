using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes;
using Application.Telemetry;
using Domain.Cooking;
using Domain.Shared;
using Response = Contracts.CookSessions.Response;

namespace Application.CookSessions;

/// <summary>What this person is cooking, if anything.</summary>
/// <param name="UserId">Whose session.</param>
public sealed record GetCurrentCookSessionQuery(Guid UserId);

internal sealed class GetCurrentCookSessionQueryHandler(
    ICookSessionRepository sessions,
    IRecipeRepository recipes,
    IHouseholdRepository households)
    : IQueryHandler<GetCurrentCookSessionQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetCurrentCookSessionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("CookSessions.GetCurrent");

        var session = await sessions
            .ActiveForUserAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return tracked.Record(Result<Response>.Failure(CookingErrors.SessionNotFound));
        }

        // The title comes along so the resume bar is one request, not two: it
        // is on screen from the moment the app boots, and a second round trip
        // there is a second round trip on every page. Asked through access
        // again, not trusted from when cooking began: somebody who has since
        // left the household, or whose kitchen stopped inheriting the recipe,
        // has nothing to resume and is not told its title.
        var recipe = await RecipeAccess
            .VisibleInAsync(recipes, households, session.RecipeId, session.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        return tracked.Record(recipe.Map(found => session.ToResponse(found.Title.Value)));
    }
}
