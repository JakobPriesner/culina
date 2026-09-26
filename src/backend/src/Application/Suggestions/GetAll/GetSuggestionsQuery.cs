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
/// <param name="Context">Who is asking, and about what.</param>
public sealed record GetSuggestionsQuery(SuggestionContext Context);

internal sealed class GetSuggestionsQueryHandler(
    ISuggestionRanker ranker,
    IHouseholdRepository households,
    IRecipeRepository recipes,
    RankingWeights weights)
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

        var member = await households
            .IsMemberAsync(context.HouseholdId, context.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!member)
        {
            // Not-found rather than forbidden, for the same reason every other
            // household read gives a non-member a 404.
            return tracked.Record(
                Result<Response>.Failure(HouseholdErrors.NotFound(context.HouseholdId)));
        }

        var library = await HouseholdAccess
            .LibraryAsync(households, context.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        // Ranked over everything this household sees, inherited recipes too.
        context = context with { InheritedFrom = [.. library.Skip(1)] };

        // "Recipes like this one" needs the one, and it has to be one of THIS
        // household's — its own or one it inherits.
        //
        // Membership alone is not enough, because a person may belong to
        // several households: naming a recipe from their other kitchen would
        // pass a visibility check, find no features inside this one, and return
        // an ordinary ranking that silently claims to resemble something. A
        // wrong answer that looks right is worse than a 404.
        //
        // Answered as not-found rather than forbidden either way, so naming a
        // stranger's recipe cannot be used to learn that it exists.
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

        SuggestionMetrics.Returned(ranked.Count, context);
        tracked.Tag("culina.suggestion.count", ranked.Count);

        return tracked.Record(Result<Response>.Success(ranked.ToResponse(weights)));
    }
}
