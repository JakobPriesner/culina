using Domain.Shared;

namespace Domain.Cookbooks;

/// <summary>
/// What a smart cookbook asks for.
/// </summary>
/// <remarks>
/// <para>
/// A saved question rather than a saved answer. Nothing here records which
/// recipes match — that is worked out whenever the shelf is read, which is what
/// makes a recipe written this evening appear on it immediately, with no job to
/// run and nothing to backfill when a rule changes.
/// </para>
/// <para>
/// Every rule must hold: a shelf asking for chicken and a main course means
/// both, because the useful shelves are the narrow ones. "Either of these" is a
/// different question and a much harder editor, and nobody has asked it yet.
/// </para>
/// </remarks>
public sealed record CookbookRules
{
    /// <summary>More conditions than anybody could hold in their head.</summary>
    public const int MaxConditions = 20;

    /// <summary>The longest tag or ingredient a rule may name.</summary>
    public const int MaxTermLength = 120;

    /// <summary>A week, in minutes.</summary>
    public const int MaxMinutesCeiling = 10_080;

    private CookbookRules(
        IReadOnlyList<string> tags,
        IReadOnlyList<string> ingredients,
        int? maxMinutes)
    {
        Tags = tags;
        Ingredients = ingredients;
        MaxMinutes = maxMinutes;
    }

    /// <summary>Tag slugs a recipe must all carry.</summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>Ingredient names a recipe must all use.</summary>
    public IReadOnlyList<string> Ingredients { get; }

    /// <summary>The longest a recipe may take, or null for any length.</summary>
    public int? MaxMinutes { get; }

    /// <summary>Whether this states nothing at all.</summary>
    public bool Empty => Tags.Count == 0 && Ingredients.Count == 0 && MaxMinutes is null;

    /// <summary>The rules a manual cookbook has, which are none.</summary>
    public static readonly CookbookRules None = new([], [], null);

    /// <summary>Parses a set of rules, returning a failure rather than throwing.</summary>
    /// <param name="tags">Tag slugs a recipe must all carry.</param>
    /// <param name="ingredients">Ingredients a recipe must all use.</param>
    /// <param name="maxMinutes">The longest a recipe may take, or null.</param>
    public static Result<CookbookRules> Create(
        IReadOnlyList<string>? tags,
        IReadOnlyList<string>? ingredients,
        int? maxMinutes)
    {
        var cleanedTags = Clean(tags);
        var cleanedIngredients = Clean(ingredients);

        if (cleanedTags.Count > MaxConditions || cleanedIngredients.Count > MaxConditions)
        {
            return CookbookErrors.TooManyRules;
        }

        if (cleanedTags.Concat(cleanedIngredients).Any(term => term.Length > MaxTermLength))
        {
            return CookbookErrors.InvalidRule;
        }

        if (maxMinutes is <= 0 or > MaxMinutesCeiling)
        {
            return CookbookErrors.InvalidRule;
        }

        return new CookbookRules(cleanedTags, cleanedIngredients, maxMinutes);
    }

    /// <summary>Rebuilds rules from storage.</summary>
    /// <param name="tags">The stored tag slugs.</param>
    /// <param name="ingredients">The stored ingredient names.</param>
    /// <param name="maxMinutes">The stored ceiling.</param>
    public static CookbookRules Restore(
        IReadOnlyList<string> tags,
        IReadOnlyList<string> ingredients,
        int? maxMinutes) =>
        new(tags, ingredients, maxMinutes);

    /// <summary>
    /// Trimmed, emptied of blanks, and deduplicated case-insensitively.
    /// </summary>
    /// <remarks>
    /// The same term twice is one condition written twice, and a shelf that
    /// reported "2 rules" for it would be counting the typing rather than the
    /// question.
    /// </remarks>
    private static IReadOnlyList<string> Clean(IReadOnlyList<string>? terms)
    {
        if (terms is null)
        {
            return [];
        }

        return
        [
            .. terms
                .Select(term => term.Trim())
                .Where(term => term.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }
}

/// <summary>Which kind of shelf a cookbook is.</summary>
/// <remarks>
/// Chosen when it is made and never changed, because the two answer "why is
/// this recipe here?" differently — one says "somebody put it there" and the
/// other "it matches". A shelf that was both could not answer at all.
/// </remarks>
public enum CookbookKind
{
    /// <summary>Somebody chose what is on it.</summary>
    Manual = 0,

    /// <summary>Whatever matches its rules, worked out when it is read.</summary>
    Smart = 1
}
