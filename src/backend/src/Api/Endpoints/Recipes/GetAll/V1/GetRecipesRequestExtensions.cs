using System.Globalization;
using Api.Infrastructure;
using Application.Abstractions;
using Domain.Shared;

namespace Api.Endpoints.Recipes.GetAll.V1;

/// <summary>Reads the search criteria out of the query string.</summary>
/// <remarks>Every value is validated rather than coerced, so a client bug such as <c>limit=twenty</c> is not hidden.</remarks>
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

        // Asking a question means being answered best first, not by most recently edited.
        var ranked = !string.IsNullOrWhiteSpace(text) || ingredients.Length > 0;

        return ToSort(query["sort"], cookbookId is not null, ranked).Map(sort => new RecipeSearch(
            householdId,
            userId,
            text,
            [.. query["tag"].Where(value => !string.IsNullOrWhiteSpace(value))!],
            ingredients!,
            maxMinutes,
            cookbookId,
            // Resolved by the handler, the only thing that can read a cookbook's rules.
            Rules: null,
            sort,
            query["cursor"],
            limit ?? DefaultLimit));
    }

    // An explicit sort wins; otherwise words or ingredients rank, a cookbook with no question reads in built
    // order, and the rest is most recently touched. Relevance precedes the cookbook default (searching inside a
    // shelf should not return its table of contents). Cookbook order without a cookbook is rejected, not ignored.
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
            ("suggested", _, _) => RecipeSort.Suggested,
            ("cookbookOrder", true, _) => RecipeSort.CookbookOrder,
            ("cookbookOrder", false, _) => new FieldError(
                "sort",
                "request.unknown_parameter",
                "Sort 'cookbookOrder' needs a 'cookbookId' to be an order of."),
            _ => new FieldError(
                "sort",
                "request.unknown_parameter",
                "Sort must be one of '-updatedAt', 'title', 'totalMinutes', '-cookCount', 'relevance', "
                + "'suggested', 'cookbookOrder'.")
        };

    // Whether the reader turned a correction down: asTyped=true, or absent.
    internal static Result<bool> ReadAsTyped(this IQueryCollection query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query["asTyped"].ToString() switch
        {
            "" or "false" => false,
            "true" => true,
            _ => new FieldError("asTyped", "request.unknown_parameter", "asTyped must be 'true' or 'false'.")
        };
    }

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

    // Absence is expressed by the out parameter: a result carries a value or an error, and "nothing asked" is neither.
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
