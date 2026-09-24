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
    /// <param name="householdId">Whose recipes.</param>
    /// <param name="words">Folded query words.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>Each correctable word, and what it most likely meant.</returns>
    Task<IReadOnlyDictionary<string, string>> SpellingsAsync(
        Guid householdId,
        IReadOnlyList<string> words,
        CancellationToken cancellationToken);
}
