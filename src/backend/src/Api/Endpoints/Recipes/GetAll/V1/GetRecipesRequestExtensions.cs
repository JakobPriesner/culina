using System.Globalization;
using Api.Infrastructure;
using Application.Abstractions;
using Domain.Shared;

namespace Api.Endpoints.Recipes.GetAll.V1;

/// <summary>Reads the search criteria out of the query string.</summary>
/// <remarks>
/// Every value is validated rather than coerced. A <c>limit</c> of "twenty" is
/// a client bug, and silently treating it as the default would hide it — the
/// same reasoning as the query-parameter guard.
/// </remarks>
internal static class GetRecipesRequestExtensions
{
    private const int DefaultLimit = 24;

    internal static Result<RecipeSearch> ToRecipeSearch(this IQueryCollection query, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!Guid.TryParse(query["householdId"], CultureInfo.InvariantCulture, out var householdId))
        {
            return RequestErrors.MissingQueryParameter("householdId");
        }

        if (!TryReadNumber(query, "limit", out var limit, out var limitFailure))
        {
            return limitFailure!;
        }

        if (!TryReadNumber(query, "maxMinutes", out var maxMinutes, out var minutesFailure))
        {
            return minutesFailure!;
        }

        if (!TryReadCookbook(query, out var cookbookId, out var cookbookFailure))
        {
            return cookbookFailure!;
        }

        return ToSort(query["sort"], cookbookId is not null).Map(sort => new RecipeSearch(
            householdId,
            userId,
            query["query"],
            [.. query["tag"].Where(value => !string.IsNullOrWhiteSpace(value))!],
            [.. query["ingredient"].Where(value => !string.IsNullOrWhiteSpace(value))!],
            maxMinutes,
            cookbookId,
            // Resolved by the handler, which is the only thing that can read a
            // cookbook to find out whether it has rules.
            Rules: null,
            sort,
            query["cursor"],
            limit ?? DefaultLimit));
    }

    /// <summary>
    /// How to order the page.
    /// </summary>
    /// <remarks>
    /// Reading a cookbook defaults to the order it was built in, because that
    /// is the order somebody meant. Asking for that order without naming a
    /// cookbook is rejected rather than quietly ignored: a filter that does
    /// nothing returns the wrong data looking right.
    /// </remarks>
    private static Result<RecipeSort> ToSort(string? value, bool inACookbook) => (value, inACookbook) switch
    {
        (null or "", true) => RecipeSort.CookbookOrder,
        (null or "" or "-updatedAt", _) => RecipeSort.RecentFirst,
        ("title", _) => RecipeSort.Title,
        ("totalMinutes", _) => RecipeSort.ShortestFirst,
        ("-cookCount", _) => RecipeSort.MostCooked,
        ("relevance", _) => RecipeSort.Relevance,
        ("cookbookOrder", true) => RecipeSort.CookbookOrder,
        ("cookbookOrder", false) => new FieldError(
            "sort",
            "request.unknown_parameter",
            "Sort 'cookbookOrder' needs a 'cookbookId' to be an order of."),
        _ => new FieldError(
            "sort",
            "request.unknown_parameter",
            "Sort must be one of '-updatedAt', 'title', 'totalMinutes', '-cookCount', 'relevance', "
            + "'cookbookOrder'.")
    };

    /// <summary>Reads an optional cookbook to read inside.</summary>
    private static bool TryReadCookbook(IQueryCollection query, out Guid? value, out Error? failure)
    {
        var raw = query["cookbookId"].ToString();

        if (string.IsNullOrEmpty(raw))
        {
            value = null;
            failure = null;

            return true;
        }

        if (Guid.TryParse(raw, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            failure = null;

            return true;
        }

        value = null;
        failure = new FieldError("cookbookId", "request.unknown_parameter", "That is not a cookbook id.");

        return false;
    }

    /// <summary>
    /// Reads an optional whole number. Absence is expressed by the out
    /// parameter rather than inside a result, because a result carries a value
    /// or an error and "nothing was asked for" is neither.
    /// </summary>
    private static bool TryReadNumber(
        IQueryCollection query,
        string name,
        out int? value,
        out Error? failure)
    {
        value = null;
        failure = null;

        if (query[name].Count == 0)
        {
            return true;
        }

        if (!int.TryParse(query[name], CultureInfo.InvariantCulture, out var parsed))
        {
            failure = new FieldError(
                name,
                "request.unknown_parameter",
                $"'{name}' must be a whole number.");

            return false;
        }

        value = parsed;

        return true;
    }
}
