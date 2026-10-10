using Api.Infrastructure;
using Application.Abstractions;
using Domain.Shared;

namespace Api.Endpoints.Recipes.GetAll.V1;

/// <summary>Reads the search criteria out of the query string.</summary>
/// <remarks>Every value is validated rather than coerced, so a client bug such as <c>limit=twenty</c> is not hidden.</remarks>
internal static class GetRecipesRequestExtensions
{
    private const int DefaultLimit = 24;

    // The ceiling the searcher holds a page to.
    private const int MaxLimit = 100;

    internal static Result<RecipeSearch> ToRecipeSearch(this IQueryCollection query, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.RequireGuid("householdId").Bind(householdId =>
            query.ReadInt("limit", 1, MaxLimit, DefaultLimit).Bind(limit =>
                query.ReadInt("maxMinutes", 1).Bind(maxMinutes =>
                    query.ReadGuid("cookbookId").Bind(cookbookId =>
                        ToSearch(query, userId, householdId, limit, maxMinutes.Value, cookbookId.Value)))));
    }

    private static Result<RecipeSearch> ToSearch(
        IQueryCollection query,
        Guid userId,
        Guid householdId,
        int limit,
        int? maxMinutes,
        Guid? cookbookId)
    {
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
            limit));
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
            ("cookbookOrder", false, _) => RequestErrors.InvalidQueryParameter(
                "sort",
                "something other than 'cookbookOrder' unless a 'cookbookId' is given"),
            _ => RequestErrors.InvalidQueryParameter(
                "sort",
                "one of '-updatedAt', 'title', 'totalMinutes', '-cookCount', 'relevance', 'suggested', "
                + "'cookbookOrder'")
        };
}
