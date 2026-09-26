using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Cookbooks;
using Application.Search;
using Application.Telemetry;
using Contracts.Recipes.GetAll;
using Domain.Households;
using Domain.Search;
using Domain.Shared;
using Facet = Contracts.Recipes.GetAll.Facet;
using Facets = Contracts.Recipes.GetAll.Facets;
using MatchReason = Contracts.Recipes.GetAll.MatchReason;

namespace Application.Recipes.GetAll;

/// <summary>Finds recipes in one household.</summary>
/// <param name="Search">What to look for.</param>
/// <param name="AsTyped">
/// Search the words exactly as typed: the reader has turned a correction down.
/// </param>
public sealed record GetRecipesQuery(RecipeSearch Search, bool AsTyped = false);

internal sealed class GetRecipesQueryHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    ICookbookRepository cookbooks,
    ISearchVocabulary vocabulary)
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

        // The household's own recipes and every one it inherits, searched as
        // one library.
        var ancestors = await households
            .AncestorsAsync(query.Search.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

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
            InheritedFrom = [.. ancestors.Select(ancestor => ancestor.HouseholdId)],
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

        var (page, answered, recovery) = await SearchRecovery
            .SearchAsync(
                recipes,
                vocabulary,
                search,
                intent,
                query.Search.MaxMinutes,
                query.AsTyped,
                cancellationToken)
            .ConfigureAwait(false);

        // Counted over every result rather than the page, and only for a
        // question on its first page: a refinement is offered once, where the
        // results begin.
        var facets = page.Total > 0 && answered.Cursor is null && intent.Asked
            ? await recipes.FacetsAsync(answered, cancellationToken).ConfigureAwait(false)
            : null;

        return tracked.Record(Result<Response>.Success(
            page.ToResponse(answered, intent, recovery, facets)));
    }

    private static int? Min(int? asked, int? read) =>
        asked is { } a && read is { } r ? Math.Min(a, r) : asked ?? read;
}

/// <summary>Maps a page of search rows onto the shape this operation returns.</summary>
internal static class RecipeListMappings
{
    /// <summary>
    /// A refinement is worth offering when it leaves between a fifth and four
    /// fifths of the results.
    /// </summary>
    private const double NarrowestShare = 0.2;

    private const double WidestShare = 0.8;

    internal static Response ToResponse(
        this RecipePage page,
        RecipeSearch search,
        QueryIntent intent,
        Recovery recovery,
        SearchFacets? facets)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(recovery);

        return new Response
        {
            Items = [.. page.Items.Select(row => row.ToSummary(search.Ingredients.Count))],
            NextCursor = page.NextCursor,
            Total = page.Total,
            // Absent without a query, so the plain library listing is exactly
            // what it always was.
            Interpretation = !intent.Asked
                ? null
                : new Interpretation
                {
                    FreeText = recovery.CorrectedTo ?? intent.FreeText,
                    Applied = [.. intent.Applied.Select(one => one.ToContract())],
                    CorrectedFrom = recovery.CorrectedFrom,
                    Relaxed = recovery.Relaxed.Count == 0 ? null : [.. recovery.Relaxed.Select(one => one.ToContract())],
                    Conflict = recovery.Conflict.Count == 0 ? null : [.. recovery.Conflict.Select(one => one.ToContract())]
                },
            Facets = facets?.ToContract(search)
        };
    }

    /// <summary>
    /// The refinements worth a chip: not already applied, and neither
    /// removing nothing nor everything — the ones that best halve the results
    /// first.
    /// </summary>
    private static Facets? ToContract(this SearchFacets facets, RecipeSearch search)
    {
        bool Splits(Abstractions.Facet facet) =>
            facets.Total > 0
            && facet.Count >= NarrowestShare * facets.Total
            && facet.Count <= WidestShare * facets.Total;

        List<Facet> Best(IEnumerable<Abstractions.Facet> offered, Func<Abstractions.Facet, bool> open, int most) =>
        [
            .. offered
                .Where(facet => Splits(facet) && open(facet))
                .OrderBy(facet => Math.Abs(facet.Count - (facets.Total / 2.0)))
                .ThenBy(facet => facet.Value, StringComparer.Ordinal)
                .Take(most)
                .Select(facet => new Facet { Value = facet.Value, Label = facet.Label, Count = facet.Count })
        ];

        var tags = Best(facets.Tags, facet => !search.Tags.Contains(facet.Value, StringComparer.Ordinal), 5);
        var times = Best(
            facets.Times,
            facet => search.MaxMinutes is not { } ceiling
                     || int.Parse(facet.Value, System.Globalization.CultureInfo.InvariantCulture) < ceiling,
            2);
        var cuisines = Best(
            facets.Cuisines,
            facet => !search.Constraints.Cuisines.Contains(facet.Value, StringComparer.Ordinal),
            3);

        return tags.Count + times.Count + cuisines.Count == 0
            ? null
            : new Facets { Tags = tags, Times = times, Cuisines = cuisines };
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
        End = inference.End,
        Word = inference.Word
    };

    private static RecipeSummary ToSummary(this RecipeSearchRow row, int requestedIngredients) => new()
    {
        RecipeId = row.RecipeId,
        HouseholdId = row.HouseholdId,
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
            },
        MatchReason = row.Reason is { } reason
            ? new MatchReason
            {
                Kind = reason.Kind,
                // A concept is named in the recipe's own language, which is the
                // one the rest of its card is in.
                Term = reason.Kind == "concept" && reason.Term is { } key && CulinaryLexicon.Find(key) is { } concept
                    ? (reason.Language == "de" ? concept.De : concept.En)[0]
                    : reason.Term
            }
            : null
    };
}
