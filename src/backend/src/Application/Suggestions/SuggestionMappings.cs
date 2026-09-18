using Application.Abstractions;
using Domain.Suggestions;
using Contracts.Suggestions.GetAll;

namespace Application.Suggestions;

/// <summary>Maps ranked recipes onto the shape the suggestions endpoint returns.</summary>
internal static class SuggestionMappings
{
    internal static Response ToResponse(this IReadOnlyList<ScoredRecipe> ranked, RankingWeights weights)
    {
        ArgumentNullException.ThrowIfNull(ranked);
        ArgumentNullException.ThrowIfNull(weights);

        return new Response { Items = [.. ranked.Select(item => item.ToSuggestion(weights))] };
    }

    private static Suggestion ToSuggestion(this ScoredRecipe scored, RankingWeights weights)
    {
        var reason = SuggestionExplanation.For(scored.Terms, weights);

        return new Suggestion
        {
            RecipeId = scored.Recipe.RecipeId,
            Title = scored.Recipe.Title,
            ImageId = scored.Recipe.ImageId,
            TotalMinutes = scored.Recipe.TotalMinutes,
            YieldAmount = scored.Recipe.YieldAmount,
            YieldKind = scored.Recipe.YieldKind,
            YieldLabel = scored.Recipe.YieldLabel,
            Tags = scored.Recipe.Tags,
            CookCount = scored.Recipe.CookCount,
            LastCookedAt = scored.Recipe.LastCookedAt,
            UpdatedAt = scored.Recipe.UpdatedAt,
            // Null rather than a "none" code, so a client that forgets to
            // branch renders nothing instead of the word "none".
            Reason = reason.Reason == SuggestionReason.None
                ? null
                : new SuggestionReasonView { Code = ReasonCodes.Of(reason.Reason), Subject = reason.Subject }
        };
    }

    // The score itself never crosses the wire. It is an ordering and nothing
    // else — no unit, no scale, not comparable between two households — and
    // publishing one would invite somebody to compare two numbers that mean
    // nothing apart.
    private static class ReasonCodes
    {
        internal static string Of(SuggestionReason reason) => reason switch
        {
            SuggestionReason.Affinity => "affinity",
            SuggestionReason.Rediscovery => "rediscovery",
            SuggestionReason.Tag => "tag",
            SuggestionReason.Ingredient => "ingredient",
            SuggestionReason.Season => "season",
            SuggestionReason.Slot => "slot",
            SuggestionReason.Household => "household",
            SuggestionReason.Fresh => "fresh",
            SuggestionReason.Similar => "similar",
            _ => "none"
        };
    }
}
