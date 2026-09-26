namespace Application.Abstractions;

/// <summary>
/// The words one household's recipes are written in.
/// </summary>
/// <remarks>
/// A household's own vocabulary is the right dictionary for correcting a
/// search, and a better one than any word list: it offers "Bolognese" for
/// "Bolgnese" because that is what is actually there, and offers nothing at
/// all for a word no recipe in the kitchen uses — which is the correct answer.
/// </remarks>
public interface ISearchVocabulary
{
    /// <summary>
    /// The household's nearest word to each of some folded words that its
    /// recipes do not use, where one is near enough.
    /// </summary>
    /// <param name="library">Whose recipes: a household, then every household it inherits from.</param>
    /// <param name="words">Folded query words.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>Each correctable word, and what it most likely meant.</returns>
    Task<IReadOnlyDictionary<string, string>> SpellingsAsync(
        IReadOnlyList<Guid> library,
        IReadOnlyList<string> words,
        CancellationToken cancellationToken);

    /// <summary>
    /// What a half-typed word could become: recipes, ingredients and tags of
    /// this household with a word that begins with it.
    /// </summary>
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

/// <summary>
/// An ingredient the household cooks with, how many recipes use it, and how
/// many of those are known to take half an hour or less.
/// </summary>
public sealed record IngredientCompletion(string Name, int RecipeCount, int QuickCount);

/// <summary>A tag the household uses, and on how many recipes.</summary>
public sealed record TagCompletion(string Slug, string Name, int RecipeCount);
