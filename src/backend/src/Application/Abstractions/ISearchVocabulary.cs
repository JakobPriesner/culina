namespace Application.Abstractions;

/// <summary>The words one household's recipes are written in, used to correct and complete searches.</summary>
public interface ISearchVocabulary
{
    /// <summary>The nearest used word for each folded word the recipes do not use, where one is near enough.</summary>
    /// <param name="library">Whose recipes: a household, then every household it inherits from.</param>
    /// <param name="words">Folded query words.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>Each correctable word, and what it most likely meant.</returns>
    Task<IReadOnlyDictionary<string, string>> SpellingsAsync(
        IReadOnlyList<Guid> library,
        IReadOnlyList<string> words,
        CancellationToken cancellationToken);

    /// <summary>Recipes, ingredients and tags with a word beginning with the half-typed one.</summary>
    /// <param name="library">Whose recipes: a household, then every household it inherits from.</param>
    /// <param name="typed">The word so far, as typed.</param>
    /// <param name="perKind">How many of each kind at most.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    Task<Completions> CompletionsAsync(
        IReadOnlyList<Guid> library,
        string typed,
        int perKind,
        CancellationToken cancellationToken);
}

/// <summary>What a half-typed word could become, by kind.</summary>
/// <param name="Recipes">Recipes whose title has a word beginning with it, the closest first.</param>
/// <param name="Ingredients">Ingredients by name, the most used first.</param>
/// <param name="Tags">Tags, the most used first.</param>
public sealed record Completions(
    IReadOnlyList<RecipeCompletion> Recipes,
    IReadOnlyList<IngredientCompletion> Ingredients,
    IReadOnlyList<TagCompletion> Tags);

/// <summary>A recipe to go straight to.</summary>
public sealed record RecipeCompletion(Guid RecipeId, string Title, Guid? ImageId, int? TotalMinutes);

/// <summary>An ingredient, how many recipes use it, and how many of those take half an hour or less.</summary>
public sealed record IngredientCompletion(string Name, int RecipeCount, int QuickCount);

/// <summary>A tag the household uses, and on how many recipes.</summary>
public sealed record TagCompletion(string Slug, string Name, int RecipeCount);
