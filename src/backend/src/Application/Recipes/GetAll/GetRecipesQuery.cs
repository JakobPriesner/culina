using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Cookbooks;
using Application.Search;
using Application.Telemetry;
using Contracts.Recipes.GetAll;
using Domain.Households;
using Domain.Shared;

namespace Application.Recipes.GetAll;

/// <summary>Finds recipes in one household.</summary>
/// <param name="Search">What to look for.</param>
public sealed record GetRecipesQuery(RecipeSearch Search);

internal sealed class GetRecipesQueryHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    ICookbookRepository cookbooks)
    : IQueryHandler<GetRecipesQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetRecipesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetAll");

        var member = await households
            .IsMemberAsync(query.Search.HouseholdId, query.Search.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!member)
        {
            // Not-found rather than forbidden, for the same reason every other
            // household read gives a non-member a 404.
            return tracked.Record(
                Result<Response>.Failure(HouseholdErrors.NotFound(query.Search.HouseholdId)));
        }

        // Reading inside a cookbook means one of two things, and only the
        // cookbook knows which: a shelf somebody filled names rows, and one
        // that fills itself names conditions. Resolved here so the searcher
        // never has to know what a cookbook is.
        var scope = await CookbookScope
            .ResolveAsync(
                cookbooks,
                query.Search.CookbookId,
                query.Search.HouseholdId,
                cancellationToken)
            .ConfigureAwait(false);

        // What the words ask for beyond themselves — a diet, a time, a meal,
        // what to use and what to leave out — read before the search runs, so
        // the lanes are handed only the words still to be found.
        var intent = QueryUnderstanding.Parse(query.Search.Query);

        var search = query.Search with
        {
            Query = intent.FreeText,
            Ingredients = [.. query.Search.Ingredients, .. intent.Ingredients],
            MaxMinutes = Min(query.Search.MaxMinutes, intent.MaxMinutes),
            Constraints = intent.ToConstraints(),
            CookbookId = scope.Membership,
            Rules = scope.Rules,
            // A shelf that fills itself was never put in an order, so it falls
            // back to the default rather than ordering by a column that is null
            // for every row on it.
            Sort = query.Search.Sort == RecipeSort.CookbookOrder && !scope.Ordered
                ? RecipeSort.RecentFirst
                : query.Search.Sort
        };

        var page = await recipes.SearchAsync(search, cancellationToken).ConfigureAwait(false);

        return tracked.Record(Result<Response>.Success(page.ToResponse(search, intent)));
    }

    private static int? Min(int? asked, int? read) =>
        asked is { } a && read is { } r ? Math.Min(a, r) : asked ?? read;
}

/// <summary>Maps a page of search rows onto the shape this operation returns.</summary>
internal static class RecipeListMappings
{
    internal static Response ToResponse(this RecipePage page, RecipeSearch search, QueryIntent intent)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(intent);

        return new Response
        {
            Items = [.. page.Items.Select(row => row.ToSummary(search.Ingredients.Count))],
            NextCursor = page.NextCursor,
            Total = page.Total,
            // Absent without a query, so the plain library listing is exactly
            // what it always was.
            Interpretation = intent.Applied.Count == 0 && intent.FreeText.Length == 0
                ? null
                : new Interpretation
                {
                    FreeText = intent.FreeText,
                    Applied = [.. intent.Applied.Select(one => one.ToContract())]
                }
        };
    }

    private static AppliedInference ToContract(this Inference inference) => new()
    {
        Kind = inference.Kind switch
        {
            InferenceKind.Time => "time",
            InferenceKind.Quick => "quick",
            InferenceKind.Diet => "diet",
            InferenceKind.Meal => "meal",
            InferenceKind.Cuisine => "cuisine",
            InferenceKind.Ingredient => "ingredient",
            _ => "exclusion"
        },
        Value = inference.Value,
        Text = inference.Text,
        Start = inference.Start,
        End = inference.End
    };

    private static RecipeSummary ToSummary(this RecipeSearchRow row, int requestedIngredients) => new()
    {
        RecipeId = row.RecipeId,
        Title = row.Title,
        ImageId = row.ImageId,
        TotalMinutes = row.TotalMinutes,
        YieldAmount = row.YieldAmount,
        YieldKind = row.YieldKind,
        YieldLabel = row.YieldLabel,
        Tags = row.Tags,
        CookCount = row.CookCount,
        LastCookedAt = row.LastCookedAt,
        UpdatedAt = row.UpdatedAt,
        // Omitted entirely when the caller named no ingredients, so a plain
        // browse is not cluttered with "uses 0 of 0".
        IngredientMatch = requestedIngredients == 0
            ? null
            : new IngredientMatch
            {
                Matched = row.MatchedIngredients,
                Requested = requestedIngredients,
                Missing = Math.Max(row.IngredientCount - row.MatchedIngredients, 0)
            }
    };
}
