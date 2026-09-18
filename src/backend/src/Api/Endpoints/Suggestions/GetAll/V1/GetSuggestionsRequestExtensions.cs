using System.Globalization;
using Api.Infrastructure;
using Application.Abstractions;
using Domain.Planning;
using Domain.Shared;
using Domain.Suggestions;

namespace Api.Endpoints.Suggestions.GetAll.V1;

/// <summary>Reads the occasion out of the query string.</summary>
/// <remarks>
/// Every value is validated rather than coerced, exactly as the recipe search
/// reads its criteria: a <c>slot</c> of "brunch" is a client bug, and quietly
/// ignoring it would answer with a dinner list that looks like a brunch list.
/// </remarks>
internal static class GetSuggestionsRequestExtensions
{
    internal static Result<SuggestionContext> ToSuggestionContext(
        this IQueryCollection query,
        Guid userId,
        DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!Guid.TryParse(query["householdId"], CultureInfo.InvariantCulture, out var householdId))
        {
            return RequestErrors.MissingQueryParameter("householdId");
        }

        if (!TryReadId(query["likeRecipeId"], "likeRecipeId", out var likeRecipeId, out var likeFailure))
        {
            return likeFailure!;
        }

        if (!TryReadPurpose(query["purpose"], likeRecipeId, out var purpose, out var purposeFailure))
        {
            return purposeFailure!;
        }

        if (!TryReadSlot(query["slot"], out var slot, out var slotFailure))
        {
            return slotFailure!;
        }

        if (!TryReadLimit(query["limit"], out var limit, out var limitFailure))
        {
            return limitFailure!;
        }

        if (!TryReadMinutes(query["maxMinutes"], out var maxMinutes, out var minutesFailure))
        {
            return minutesFailure!;
        }

        if (!TryReadIds(query["exclude"], out var exclude, out var excludeFailure))
        {
            return excludeFailure!;
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

    /// <summary>
    /// Which question is being asked.
    /// </summary>
    /// <remarks>
    /// Naming a recipe to resemble <i>is</i> asking for "like", so a caller does
    /// not have to say both — but asking for "like" without naming one is
    /// refused rather than quietly answered with something else, because a list
    /// that claims to resemble nothing in particular is worse than an error.
    /// <para>
    /// Browse is deliberately not nameable here. Ranking the whole collection is
    /// a sort on the collection, and two ways to ask one question is how the two
    /// end up giving different answers.
    /// </para>
    /// </remarks>
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

    private static bool TryReadLimit(string? value, out int limit, out Error? failure)
    {
        limit = SuggestionContext.DefaultCount;
        failure = null;

        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (!int.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
        {
            failure = new FieldError("limit", "request.unknown_parameter", "'limit' must be a whole number.");

            return false;
        }

        // Rejected rather than clamped. Asking for fifty is a client that thinks
        // this is the recipe list, and quietly handing back twelve would leave
        // that client believing the kitchen is nearly empty.
        if (parsed is < 1 or > SuggestionContext.MaxCount)
        {
            failure = SuggestionErrors.InvalidLimit;

            return false;
        }

        limit = parsed;

        return true;
    }

    private static bool TryReadMinutes(string? value, out int? minutes, out Error? failure)
    {
        minutes = null;
        failure = null;

        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (!int.TryParse(value, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            failure = new FieldError(
                "maxMinutes",
                "request.unknown_parameter",
                "'maxMinutes' must be a whole number of minutes greater than zero.");

            return false;
        }

        minutes = parsed;

        return true;
    }

    private static bool TryReadId(string? value, string name, out Guid? id, out Error? failure)
    {
        id = null;
        failure = null;

        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (!Guid.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
        {
            failure = new FieldError(name, "request.unknown_parameter", $"'{name}' is not a recipe id.");

            return false;
        }

        id = parsed;

        return true;
    }

    private static bool TryReadIds(
        IReadOnlyList<string?> values,
        out IReadOnlyList<Guid> ids,
        out Error? failure)
    {
        List<Guid> parsed = [];

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!Guid.TryParse(value, CultureInfo.InvariantCulture, out var id))
            {
                ids = [];
                failure = new FieldError(
                    "exclude",
                    "request.unknown_parameter",
                    "Every 'exclude' must be a recipe id.");

                return false;
            }

            parsed.Add(id);
        }

        ids = parsed;
        failure = null;

        return true;
    }
}
