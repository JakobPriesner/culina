using Application.Abstractions;
using Contracts.Suggestions.GetAll;
using Domain.Suggestions;

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
            Calories = scored.Recipe.Calories is { } energy
                ? new Contracts.Recipes.GetNutrition.NutritionValue { Value = energy.Value, AtLeast = energy.AtLeast, Estimated = energy.Estimated }
                : null,
            RecipeId = scored.Recipe.RecipeId,
            HouseholdId = scored.Recipe.HouseholdId,
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
            // Null, not a "none" code, so a client that forgets to branch renders nothing.
            Reason = reason.Reason == SuggestionReason.None
                ? null
                : new SuggestionReasonView { Code = ReasonCodes.Of(reason.Reason), Subject = reason.Subject }
        };
    }

    // The score never crosses the wire: it is an ordering, with no unit or scale, not comparable between households.
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
