using Domain.Cooking;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes one person's notes on a recipe.</summary>
public interface IPersonalNoteRepository
{
    /// <summary>Every note this person has written on this recipe.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="userId">Whose notes.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<PersonalNote>> ForRecipeAsync(
        Guid recipeId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces this person's notes on this recipe with the ones supplied.
    /// </summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="userId">Whose notes.</param>
    /// <param name="notes">The notes to keep.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// A replacement, because the notes panel edits them together and an empty
    /// note means "delete this one" rather than "store a blank".
    /// </remarks>
    Task<Result> ReplaceAsync(
        Guid recipeId,
        Guid userId,
        IReadOnlyList<PersonalNote> notes,
        CancellationToken cancellationToken);
}
