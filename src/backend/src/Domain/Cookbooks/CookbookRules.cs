using Domain.Shared;

namespace Domain.Cookbooks;

/// <summary>
/// What a smart cookbook asks for: a saved question, evaluated whenever the shelf is read.
/// Every rule must hold (AND).
/// </summary>
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

    private static IReadOnlyList<string> Clean(IReadOnlyList<string>? terms)
    {
        if (terms is null)
        {
            return [];
        }

        return
        [
            .. terms
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .Select(term => term.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }
}

/// <summary>Which kind of shelf a cookbook is.</summary>
/// <remarks>Fixed at creation: the two answer "why is this recipe here?" differently.</remarks>
public enum CookbookKind
{
    /// <summary>Somebody chose what is on it.</summary>
    Manual = 0,

    /// <summary>Whatever matches its rules, worked out when it is read.</summary>
    Smart = 1
}
