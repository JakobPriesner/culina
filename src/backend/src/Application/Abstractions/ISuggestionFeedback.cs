using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Records what somebody said "not this" to: the one signal the ranker cannot derive, since with two to eight people there is no meaningful non-click.</summary>
public interface ISuggestionFeedback
{
    /// <summary>Hides a recipe from this person's suggestions; the expiry is measured from <c>at</c>.</summary>
    /// <remarks>Idempotent and keeps the first moment: a double tap or retry means what it looks like, and "not tonight" was said once.</remarks>
    Task<Result> DismissAsync(
        Guid userId,
        Guid recipeId,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>Takes a dismissal back (the undo behind the toast); taking back one that was never there is a success, not a 404.</summary>
    Task<Result> RestoreAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken);
}
