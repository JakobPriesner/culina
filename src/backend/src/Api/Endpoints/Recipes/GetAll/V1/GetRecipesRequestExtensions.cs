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

        return ToSort(query["sort"]).Map(sort => new RecipeSearch(
            householdId,
            userId,
            query["query"],
            [.. query["tag"].Where(value => !string.IsNullOrWhiteSpace(value))!],
            [.. query["ingredient"].Where(value => !string.IsNullOrWhiteSpace(value))!],
            maxMinutes,
            sort,
            query["cursor"],
            limit ?? DefaultLimit));
    }

    private static Result<RecipeSort> ToSort(string? value) => value switch
    {
        null or "" or "-updatedAt" => RecipeSort.RecentFirst,
        "title" => RecipeSort.Title,
        "totalMinutes" => RecipeSort.ShortestFirst,
        "-cookCount" => RecipeSort.MostCooked,
        "relevance" => RecipeSort.Relevance,
        _ => new FieldError(
            "sort",
            "request.unknown_parameter",
            "Sort must be one of '-updatedAt', 'title', 'totalMinutes', '-cookCount', 'relevance'.")
    };

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
