using System.Text.RegularExpressions;
using Application.Abstractions;
using Dapper;
using Domain.Search;
using Domain.Suggestions;
using Infrastructure.Persistence.Suggestions;

namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// The named parameters a search statement is run with.
/// </summary>
/// <remarks>
/// Apart from <see cref="RecipeSearcher"/> because it is a long list that has
/// nothing to do with the SQL it is bound to, and the searcher is already the
/// biggest file of its folder. The statement names every one of these.
/// </remarks>
/// <param name="time">The clock the suggested order is ranked against.</param>
/// <param name="weights">What each term of the suggested order is worth.</param>
internal sealed partial class RecipeSearchParameters(TimeProvider time, RankingWeights weights)
{
    internal DynamicParameters Build(RecipeSearch search, RecipeCursor? cursor, bool scored)
    {
        // Counted after de-duplication, because the clause compares this to a
        // count of distinct slugs: ?tag=quick&tag=quick would otherwise ask for
        // two of a tag a recipe can only carry once, and match nothing.
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
            cookbookId = search.CookbookId,
            ruleTags = search.Rules?.Tags.Distinct(StringComparer.Ordinal).ToArray() ?? [],
            ruleIngredients = search.Rules?.Ingredients.ToArray() ?? [],
            ruleMaxMinutes = search.Rules?.MaxMinutes,
            cursorId = cursor?.Id ?? Guid.Empty,
            k0 = KeyAt(cursor, 0),
            k1 = KeyAt(cursor, 1),
            k2 = KeyAt(cursor, 2)
        });

        if (scored)
        {
            // The same occasion the suggestion endpoint builds, minus the parts
            // a library listing cannot know: no slot, nothing to resemble,
            // nothing on screen to avoid. Browse also turns the exploration
            // jitter off, because a jittered order is not one a cursor can
            // resume.
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
    /// The concepts a query names, for the concept lane.
    /// </summary>
    /// <remarks>
    /// A word with a minus in front of it is one the query asked not to have,
    /// so it names nothing to look for: "Tomaten -Reis" must not bring in every
    /// rice dish by what rice is.
    /// </remarks>
    private static string[] ConceptsAskedFor(string? query) =>
        query is null
            ? []
            : [.. CulinaryLexicon.Recognise(Excluded().Replace(query, " ")).Order(StringComparer.Ordinal)];

    [GeneratedRegex(@"(?<!\S)-\S+")]
    private static partial Regex Excluded();

    /// <summary>
    /// The day being ranked for, not the instant.
    /// </summary>
    /// <remarks>
    /// Every decayed term is a function of this, so two requests on the same day
    /// score identically — which is what lets a cursor resume the order it was
    /// cut from, and what stops the list reordering under somebody who is still
    /// reading it.
    /// </remarks>
    private DateTimeOffset Today()
    {
        var now = time.GetUtcNow();

        return new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
    }

    private static string KeyAt(RecipeCursor? cursor, int index) =>
        cursor is not null && index < cursor.Keys.Count ? cursor.Keys[index] : string.Empty;
}
