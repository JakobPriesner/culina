using Application.Abstractions;
using Domain.Search;

namespace Application.Search;

/// <summary>What a search had to change to find anything, so the reader can be told.</summary>
/// <param name="CorrectedFrom">The words as typed, when they were corrected.</param>
/// <param name="CorrectedTo">What they were corrected to.</param>
/// <param name="Relaxed">The readings set aside to find something.</param>
/// <param name="Conflict">Two readings that cannot both hold, when there are.</param>
public sealed record Recovery(
    string? CorrectedFrom,
    string? CorrectedTo,
    IReadOnlyList<Inference> Relaxed,
    IReadOnlyList<Inference> Conflict)
{
    /// <summary>Nothing had to change.</summary>
    public static Recovery None { get; } = new(null, null, [], []);
}

/// <summary>What a search does when the query as typed finds nothing.</summary>
/// <remarks>
/// An empty first page walks a short ladder; each rung changes one thing and the response says
/// which, so a fallback is never unlabelled. A diet or an exclusion is never set aside.
/// </remarks>
public static class SearchRecovery
{
    /// <summary>The order readings are set aside in: the weakest guess first.</summary>
    private static readonly InferenceKind[] Relaxable =
        [InferenceKind.Cuisine, InferenceKind.Meal, InferenceKind.Ingredient, InferenceKind.Time];

    /// <summary>
    /// Two readings that cannot both hold: a diet and an ingredient it rules out, or an ingredient
    /// also excluded.
    /// </summary>
    /// <remarks>
    /// Found by reading the query, not searching it, so the honest reason can be shown with a way
    /// to drop either half.
    /// </remarks>
    public static IReadOnlyList<Inference> ConflictIn(QueryIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        foreach (var ingredient in intent.Of(InferenceKind.Ingredient))
        {
            var lineage = CulinaryLexicon.Lineage(ingredient.Value);

            foreach (var diet in intent.Of(InferenceKind.Diet))
            {
                if (DietRules.RefutedBy(diet.Value) is { } refuted && lineage.Intersect(refuted).Any())
                {
                    return [diet, ingredient];
                }
            }

            foreach (var exclusion in intent.Of(InferenceKind.Exclusion))
            {
                if (lineage.Contains(exclusion.Value, StringComparer.Ordinal))
                {
                    return [ingredient, exclusion];
                }
            }
        }

        return [];
    }

    /// <summary>Searches, and when the first page is empty, tries the rungs in turn.</summary>
    /// <param name="recipes">Where to search.</param>
    /// <param name="vocabulary">Where a misspelling is corrected against.</param>
    /// <param name="search">The search, already carrying what the query was read to mean.</param>
    /// <param name="intent">What the query was read to mean.</param>
    /// <param name="askedMaxMinutes">
    /// A time ceiling set as a parameter, which setting the query's own aside leaves standing.
    /// </param>
    /// <param name="asTyped">
    /// The reader turned a correction down: search exactly what they typed.
    /// </param>
    /// <param name="cancellationToken">Cancels the search.</param>
    public static async Task<(RecipePage Page, RecipeSearch Search, Recovery Recovery)> SearchAsync(
        IRecipeRepository recipes,
        ISearchVocabulary vocabulary,
        RecipeSearch search,
        QueryIntent intent,
        int? askedMaxMinutes,
        bool asTyped,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(intent);

        var page = await recipes.SearchAsync(search, cancellationToken).ConfigureAwait(false);

        // Only a first page: an empty later page is simply the end.
        if (page.Total > 0 || search.Cursor is not null || intent.Applied.Count == 0 && intent.FreeText.Length == 0)
        {
            return (page, search, Recovery.None);
        }

        var conflict = ConflictIn(intent);

        if (conflict.Count > 0)
        {
            return (page, search, Recovery.None with { Conflict = conflict });
        }

        if (!asTyped && await CorrectAsync(vocabulary, search, intent, cancellationToken).ConfigureAwait(false)
                is { } corrected)
        {
            var found = await recipes.SearchAsync(corrected, cancellationToken).ConfigureAwait(false);

            if (found.Total > 0)
            {
                return (found, corrected, Recovery.None with
                {
                    CorrectedFrom = intent.FreeText,
                    CorrectedTo = corrected.Query
                });
            }
        }

        foreach (var kind in Relaxable)
        {
            var relaxed = intent.Of(kind).ToList();

            if (relaxed.Count == 0)
            {
                continue;
            }

            var looser = Without(search, kind, askedMaxMinutes);
            var found = await recipes.SearchAsync(looser, cancellationToken).ConfigureAwait(false);

            if (found.Total > 0)
            {
                return (found, looser, Recovery.None with { Relaxed = relaxed });
            }
        }

        return (page, search, Recovery.None);
    }

    /// <summary>
    /// The search with each misspelt word replaced by the household's nearest, or null when none is
    /// near enough.
    /// </summary>
    private static async Task<RecipeSearch?> CorrectAsync(
        ISearchVocabulary vocabulary,
        RecipeSearch search,
        QueryIntent intent,
        CancellationToken cancellationToken)
    {
        var words = intent.FreeText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var folded = words.Select(SearchText.FoldAe).Where(word => word.Length >= 4).Distinct().ToList();

        if (folded.Count == 0)
        {
            return null;
        }

        var spellings = await vocabulary
            .SpellingsAsync(search.Library, folded, cancellationToken)
            .ConfigureAwait(false);

        if (spellings.Count == 0)
        {
            return null;
        }

        var corrected = words.Select(word =>
            spellings.TryGetValue(SearchText.FoldAe(word), out var meant) ? Cased(meant, word) : word);

        return search with { Query = string.Join(' ', corrected) };
    }

    /// <summary>The correction, capitalised the way the word it replaces was.</summary>
    private static string Cased(string meant, string typed) =>
        typed.Length > 0 && char.IsUpper(typed[0]) ? char.ToUpperInvariant(meant[0]) + meant[1..] : meant;

    private static RecipeSearch Without(RecipeSearch search, InferenceKind kind, int? askedMaxMinutes)
    {
        var constraints = search.Constraints;

        return kind switch
        {
            InferenceKind.Cuisine => search with { Constraints = constraints with { Cuisines = [] } },
            // Still preferred, so the curry stays above the yoghurt bowl.
            InferenceKind.Meal => search with
            {
                Constraints = constraints with { Meals = [], PreferredMeals = constraints.Meals }
            },
            // Still counted by the ranking; only no longer required.
            InferenceKind.Ingredient => search with { Constraints = constraints with { Ingredients = [] } },
            _ => search with { MaxMinutes = askedMaxMinutes }
        };
    }
}
