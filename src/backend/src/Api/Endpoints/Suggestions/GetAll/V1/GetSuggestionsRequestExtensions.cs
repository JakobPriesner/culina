using Api.Infrastructure;
using Application.Abstractions;
using Domain.Planning;
using Domain.Shared;
using Domain.Suggestions;

namespace Api.Endpoints.Suggestions.GetAll.V1;

/// <summary>Reads the occasion out of the query string. Values are validated, not coerced: an ignored <c>slot</c> of "brunch" would answer with a dinner list.</summary>
internal static class GetSuggestionsRequestExtensions
{
    internal static Result<SuggestionContext> ToSuggestionContext(
        this IQueryCollection query,
        Guid userId,
        DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.RequireGuid("householdId").Bind(householdId =>
            query.ReadGuid("likeRecipeId").Bind(likeRecipeId =>
                query.ReadInt("limit", 1, SuggestionContext.MaxCount, SuggestionContext.DefaultCount).Bind(limit =>
                    query.ReadInt("maxMinutes", 1).Bind(maxMinutes =>
                        query.ReadGuids("exclude").Bind(exclude =>
                            ToContext(query, userId, asOf, householdId, likeRecipeId.Value, limit, maxMinutes.Value, exclude))))));
    }

    private static Result<SuggestionContext> ToContext(
        IQueryCollection query,
        Guid userId,
        DateTimeOffset asOf,
        Guid householdId,
        Guid? likeRecipeId,
        int limit,
        int? maxMinutes,
        IReadOnlyList<Guid> exclude)
    {
        if (!TryReadPurpose(query["purpose"], likeRecipeId, out var purpose, out var purposeFailure))
        {
            return purposeFailure!;
        }

        if (!TryReadSlot(query["slot"], out var slot, out var slotFailure))
        {
            return slotFailure!;
        }

        return new SuggestionContext(
            householdId,
            userId,
            purpose,
            asOf,
            slot,
            maxMinutes,
            [.. query["tag"].Where(value => !string.IsNullOrWhiteSpace(value))!],
            [.. query["ingredient"].Where(value => !string.IsNullOrWhiteSpace(value))!],
            likeRecipeId,
            exclude,
            limit);
    }

    // Which question is being asked. A recipe to resemble implies "like", and "like" without one is refused. Browse is not nameable:
    // ranking the whole collection is a sort, and two ways to ask one question drift apart.
    private static bool TryReadPurpose(
        string? value,
        Guid? likeRecipeId,
        out SuggestionPurpose purpose,
        out Error? failure)
    {
        purpose = likeRecipeId is null ? SuggestionPurpose.Decide : SuggestionPurpose.Like;
        failure = null;

        switch (value)
        {
            case null or "" or "decide":
                return true;
            case "like" when likeRecipeId is not null:
                return true;
            case "like":
                failure = SuggestionErrors.MissingLikeRecipe;

                return false;
            default:
                failure = SuggestionErrors.UnknownPurpose;

                return false;
        }
    }

    private static bool TryReadSlot(string? value, out MealSlot? slot, out Error? failure)
    {
        slot = null;
        failure = null;

        switch (value)
        {
            case null or "":
                return true;
            case "breakfast":
                slot = MealSlot.Breakfast;

                return true;
            case "lunch":
                slot = MealSlot.Lunch;

                return true;
            case "dinner":
                slot = MealSlot.Dinner;

                return true;
            default:
                failure = SuggestionErrors.UnknownSlot;

                return false;
        }
    }
}
