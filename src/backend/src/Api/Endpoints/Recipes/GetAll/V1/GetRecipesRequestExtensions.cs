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

        var text = query["query"].ToString();
        var ingredients = query["ingredient"]
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        // Asking a question is asking to be answered best first. Without this a
        // search fell back to "most recently edited", so typing "Bolognese"
        // into a library with three of them returned whichever one somebody had
        // last fixed a typo in.
        var ranked = !string.IsNullOrWhiteSpace(text) || ingredients.Length > 0;

        return ToSort(query["sort"], cookbookId is not null, ranked).Map(sort => new RecipeSearch(
            householdId,
            userId,
            text,
            [.. query["tag"].Where(value => !string.IsNullOrWhiteSpace(value))!],
            ingredients!,
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
    /// <para>
    /// An explicit sort always wins. Where none is given the question decides:
    /// words or ingredients are a request to be ranked by them, a cookbook with
    /// no question is read in the order somebody built it, and everything else
    /// is the collection, most recently touched first.
    /// </para>
    /// <para>
    /// Relevance is checked before the cookbook default on purpose. Searching
    /// inside a shelf and being handed its table of contents is the wrong
    /// answer to a question that was plainly asked.
    /// </para>
    /// <para>
    /// Asking for cookbook order without naming a cookbook is rejected rather
    /// than quietly ignored: a filter that does nothing returns the wrong data
    /// looking right.
    /// </para>
    /// </remarks>
    private static Result<RecipeSort> ToSort(string? value, bool inACookbook, bool ranked) =>
        (value, inACookbook, ranked) switch
        {
            (null or "", _, true) => RecipeSort.Relevance,
            (null or "", true, _) => RecipeSort.CookbookOrder,
            (null or "" or "-updatedAt", _, _) => RecipeSort.RecentFirst,
            ("title", _, _) => RecipeSort.Title,
            ("totalMinutes", _, _) => RecipeSort.ShortestFirst,
            ("-cookCount", _, _) => RecipeSort.MostCooked,
            ("relevance", _, _) => RecipeSort.Relevance,
            ("cookbookOrder", true, _) => RecipeSort.CookbookOrder,
            ("cookbookOrder", false, _) => new FieldError(
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
