using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Records what somebody said "not this" to.</summary>
/// <remarks>
/// The only signal the suggestion ranker cannot derive from data another
/// feature already writes. It has to be asked for because with two to eight
/// people there is no meaningful non-click: five suggestions ignored on one
/// evening is one data point about one evening, and reading dislike into it
/// would be manufacturing data.
/// </remarks>
public interface ISuggestionFeedback
{
    /// <summary>Hides a recipe from this person's suggestions.</summary>
    /// <param name="userId">Whose suggestions.</param>
    /// <param name="recipeId">Which recipe. Must be one they can see.</param>
    /// <param name="at">When they said so — what the expiry is measured from.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// Idempotent, and it keeps the first moment: dismissing twice is one
    /// dismissal, so a double tap and a retried request both mean what they
    /// look like. Re-dismissing does not restart the clock either, because "not
    /// tonight" was said once.
    /// </remarks>
    Task<Result> DismissAsync(
        Guid userId,
        Guid recipeId,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>Takes a dismissal back.</summary>
    /// <param name="userId">Whose suggestions.</param>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// This is the undo behind the toast, so it has to be as cheap and as
    /// forgiving as the dismissal: taking back one that was never there is a
    /// success, not a 404.
    /// </remarks>
    Task<Result> RestoreAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken);
}
