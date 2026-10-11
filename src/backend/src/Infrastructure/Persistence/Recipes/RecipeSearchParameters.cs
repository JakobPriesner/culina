using System.Text.RegularExpressions;
using Application.Abstractions;
using Dapper;
using Domain.Search;
using Domain.Suggestions;
using Infrastructure.Persistence.Suggestions;

namespace Infrastructure.Persistence.Recipes;

/// <summary>The named parameters a search statement is run with.</summary>
internal sealed partial class RecipeSearchParameters(TimeProvider time, RankingWeights weights)
{
    internal DynamicParameters Build(RecipeSearch search, RecipeCursor? cursor, bool scored)
    {
        // De-duplicated first: the clause compares against a count of distinct slugs.
        var tags = search.Tags.Distinct(StringComparer.Ordinal).ToArray();
        var ingredients = search.Ingredients.ToArray();

        var query = string.IsNullOrWhiteSpace(search.Query) ? null : search.Query.Trim();
        var constraints = search.Constraints;
        var concepts = ConceptsAskedFor(query);

        var parameters = new DynamicParameters(new
        {
            householdId = search.HouseholdId,
            library = search.Library.ToArray(),
            userId = search.UserId,
            query,
            concepts,
            conceptAnswers = concepts.SelectMany(CulinaryLexicon.AnsweredBy).ToArray(),
            tagWords = query is null ? [] : SearchText.Modifiers(Excluded().Replace(query, " ")).Distinct().ToArray(),
            conceptAsked = concepts
                .SelectMany((concept, asked) => CulinaryLexicon.AnsweredBy(concept).Select(_ => asked))
                .ToArray(),
            diets = constraints.Diets.ToArray(),
            dietPresumable = constraints.Diets.All(diet => DietRules.RefutedBy(diet) is not null),
            dietRefutedBy = constraints.Diets
                .SelectMany(diet => DietRules.RefutedBy(diet) ?? [])
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            meals = constraints.Meals.ToArray(),
            cuisines = constraints.Cuisines.ToArray(),
            ingredientConcepts = constraints.Ingredients.ToArray(),
            excludedConcepts = constraints.ExcludedConcepts.ToArray(),
            excludedTerms = constraints.ExcludedTerms.ToArray(),
            quick = constraints.Quick,
            mealsLike = constraints.PreferredMeals.SelectMany(MealRules.LookLike).Distinct().ToArray(),
            mealsUnlike = constraints.PreferredMeals
                .SelectMany(MealRules.Unlike)
                .Except(constraints.PreferredMeals.SelectMany(MealRules.LookLike))
                .ToArray(),
            fuzzyThreshold = RecipeSearcher.FuzzyThreshold,
            tags,
            tagCount = tags.Length,
            ingredients,
            ingredientCount = ingredients.Length,
            maxMinutes = search.MaxMinutes,
            filterCalories = search.MaxKcal is not null,
            cookbookId = search.CookbookId,
            ruleTags = search.Rules?.Tags.Distinct(StringComparer.Ordinal).ToArray() ?? [],
            ruleIngredients = search.Rules?.Ingredients.ToArray() ?? [],
            ruleMaxMinutes = search.Rules?.MaxMinutes,
            cursorId = cursor?.Id ?? Guid.Empty,
            k0 = KeyAt(cursor, 0),
            k1 = KeyAt(cursor, 1),
            k2 = KeyAt(cursor, 2)
        });

        parameters.Add("calorieIds", Array.Empty<Guid>());

        if (scored)
        {
            // The suggestion endpoint's occasion minus slot, resemblance and exclusions.
            // Browse turns the jitter off: a cursor cannot resume a jittered order.
            parameters.AddDynamicParams(
                SuggestionScoringSql.Parameters(
                    new SuggestionContext(
                        search.HouseholdId,
                        search.UserId,
                        SuggestionPurpose.Browse,
                        Today(),
                        Slot: null,
                        search.MaxMinutes,
                        search.Tags,
                        search.Ingredients,
                        LikeRecipeId: null,
                        Exclude: [],
                        search.Limit)
                    {
                        InheritedFrom = search.InheritedFrom
                    },
                    weights));
        }

        return parameters;
    }

    /// <summary>
    /// The concepts a query names, for the concept lane; a <c>-word</c> names nothing to look for.
    /// </summary>
    private static string[] ConceptsAskedFor(string? query) =>
        query is null
            ? []
            : [.. CulinaryLexicon.Recognise(Excluded().Replace(query, " ")).Order(StringComparer.Ordinal)];

    [GeneratedRegex(@"(?<!\S)-\S+")]
    private static partial Regex Excluded();

    /// <summary>
    /// The day being ranked for, not the instant, so a cursor can resume the same order.
    /// </summary>
    private DateTimeOffset Today()
    {
        var now = time.GetUtcNow();

        return new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
    }

    private static string KeyAt(RecipeCursor? cursor, int index) =>
        cursor is not null && index < cursor.Keys.Count ? cursor.Keys[index] : string.Empty;
}
