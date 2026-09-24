using Application.Abstractions;

namespace Application.Search;

/// <summary>What kind of thing was read out of a query.</summary>
public enum InferenceKind
{
    /// <summary>A ceiling on total time: "unter 30 Minuten". A filter.</summary>
    Time,

    /// <summary>"schnell": a preference for quick recipes, never a filter.</summary>
    Quick,

    /// <summary>"vegetarisch": a diet. A filter, and never relaxed.</summary>
    Diet,

    /// <summary>"Abendessen": when it is eaten.</summary>
    Meal,

    /// <summary>"italienisch": where it comes from.</summary>
    Cuisine,

    /// <summary>"mit Kartoffeln": something the recipe should use.</summary>
    Ingredient,

    /// <summary>"ohne Zwiebeln": something it must not.</summary>
    Exclusion
}

/// <summary>
/// One thing the parser took a query to mean, and the characters it took it
/// from.
/// </summary>
/// <remarks>
/// The span is what makes a chip removable without a second parser: the
/// client deletes <c>[Start, End)</c> from the query and asks again.
/// </remarks>
/// <param name="Kind">What kind of meaning.</param>
/// <param name="Value">
/// The meaning itself: minutes for a time, a lexicon key for a diet, meal,
/// cuisine or a recognised ingredient, and the word as typed for anything the
/// lexicon does not know.
/// </param>
/// <param name="Text">The characters it was read from, as typed.</param>
/// <param name="Start">Where they begin in the query.</param>
/// <param name="End">Where they end, exclusive.</param>
/// <param name="Word">
/// For an ingredient or an exclusion, the thing itself as typed — "Kartoffeln"
/// out of "was kann ich mit Kartoffeln machen?" — which is what a chip is
/// labelled with while the span says what removing it deletes.
/// </param>
public sealed record Inference(InferenceKind Kind, string Value, string Text, int Start, int End, string? Word = null);

/// <summary>
/// A query, understood: the words still to be searched for, and everything
/// else it was found to ask.
/// </summary>
/// <param name="FreeText">What is left for the lexical lanes.</param>
/// <param name="Applied">Everything that was inferred, in the order it was typed.</param>
/// <param name="Ingredients">
/// What was asked to be used, as the names an ingredient line would say — the
/// ranking counts how many of these a recipe holds.
/// </param>
public sealed record QueryIntent(
    string FreeText,
    IReadOnlyList<Inference> Applied,
    IReadOnlyList<string> Ingredients)
{
    /// <summary>The ceiling on total time, when one was asked for.</summary>
    public int? MaxMinutes =>
        Of(InferenceKind.Time).Select(one => (int?)int.Parse(one.Value, System.Globalization.CultureInfo.InvariantCulture))
            .Min();

    /// <summary>Whether there was a question at all.</summary>
    public bool Asked => Applied.Count > 0 || FreeText.Length > 0;

    /// <summary>Whether quick recipes were asked to come first.</summary>
    public bool Quick => Of(InferenceKind.Quick).Any();

    /// <summary>The values of every inference of one kind.</summary>
    public IEnumerable<Inference> Of(InferenceKind kind) => Applied.Where(one => one.Kind == kind);

    /// <summary>What the searcher is to hold every recipe to.</summary>
    /// <remarks>
    /// An exclusion the lexicon knows is left out by what it is — "ohne
    /// Zwiebeln" also rules out the Schalotten — and one it does not is left
    /// out by name.
    /// </remarks>
    public RecipeConstraints ToConstraints() => new(
        Diets: Values(InferenceKind.Diet),
        Meals: Values(InferenceKind.Meal),
        Cuisines: Values(InferenceKind.Cuisine),
        Ingredients: Values(InferenceKind.Ingredient),
        ExcludedConcepts: [.. Of(InferenceKind.Exclusion).Select(one => one.Value).Where(Known)],
        ExcludedTerms: [.. Of(InferenceKind.Exclusion).Select(one => one.Value).Where(value => !Known(value))],
        Quick: Quick);

    private string[] Values(InferenceKind kind) => [.. Of(kind).Select(one => one.Value).Distinct(StringComparer.Ordinal)];

    private static bool Known(string value) => Domain.Search.CulinaryLexicon.Find(value) is not null;
}
