using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Contracts.Suggestions.GetAll;
using Domain.Households;
using Domain.Recipes;
using Domain.Shared;
using Domain.Suggestions;

namespace Application.Suggestions.GetAll;

/// <summary>What to suggest, and for what occasion.</summary>
public sealed record GetSuggestionsQuery(SuggestionContext Context);

internal sealed class GetSuggestionsQueryHandler(
    ISuggestionRanker ranker,
    IHouseholdRepository households,
    IRecipeRepository recipes,
    RankingWeights weights,
    IRecipeCalories calories)
    : IQueryHandler<GetSuggestionsQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetSuggestionsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Suggestions.GetAll");

        var context = query.Context;

        tracked.Tag("culina.suggestion.purpose", context.Purpose.ToString());

        // Not-found, not forbidden, as every household read gives a non-member.
        var access = await HouseholdAccess
            .LibraryOfMemberAsync(households, context.HouseholdId, context.UserId, cancellationToken)
            .ConfigureAwait(false);

        return await access.Match(
            library => RankAsync(context, library, tracked, cancellationToken),
            error => Task.FromResult(tracked.Record(Result<Response>.Failure(error)))).ConfigureAwait(false);
    }

    private async Task<Result<Response>> RankAsync(
        SuggestionContext context,
        IReadOnlyList<Guid> library,
        UseCaseActivity tracked,
        CancellationToken cancellationToken)
    {
        // Ranked over everything this household sees, inherited recipes too.
        context = context with { InheritedFrom = [.. library.Skip(1)] };

        // "Recipes like this one" needs a recipe of THIS household (own or inherited): a person in
        // several households naming one from another kitchen would pass visibility, find no
        // features here and get an ordinary ranking claiming to resemble something. Not-found
        // either way, so a stranger's recipe cannot be probed.
        if (context.LikeRecipeId is { } likeId)
        {
            var found = await recipes.FindAsync(likeId, cancellationToken).ConfigureAwait(false);

            var here = found.Match(
                recipe => library.Contains(recipe.HouseholdId),
                _ => false);

            if (!here)
            {
                return tracked.Record(Result<Response>.Failure(RecipeErrors.NotFound(likeId)));
            }
        }

        var ranked = await ranker.RankAsync(context, cancellationToken).ConfigureAwait(false);

        var figures = await calories.ReadAsync(context.HouseholdId, library,
            ranked.Select(item => item.Recipe.RecipeId).ToArray(), cancellationToken).ConfigureAwait(false);
        ranked = [.. ranked.Select(item => item with
        {
            Recipe = item.Recipe with { Calories = figures.TryGetValue(item.Recipe.RecipeId, out var energy) ? energy : null }
        })];

        SuggestionMetrics.Returned(ranked.Count, context);
        tracked.Tag("culina.suggestion.count", ranked.Count);

        return tracked.Record(Result<Response>.Success(ranked.ToResponse(weights)));
    }
}
