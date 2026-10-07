using Application.Abstractions;
using Domain.Shared;

namespace Infrastructure.Persistence.Suggestions;

/// <summary>Stores what somebody said "not this" to.</summary>
internal sealed class SuggestionFeedbackRepository(DbExecutor executor) : ISuggestionFeedback
{
    public async Task<Result> DismissAsync(
        Guid userId,
        Guid recipeId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        // do nothing on conflict: dismissing twice is one dismissal, and a restarted expiry would lengthen the silence.
        await executor.ExecuteAsync(
            """
            insert into suggestion_dismissals (user_id, recipe_id, dismissed_at)
            values (@userId, @recipeId, @at)
            on conflict (user_id, recipe_id) do nothing;
            """,
            new { userId, recipeId, at = at.UtcDateTime },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> RestoreAsync(
        Guid userId,
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        // The row count is ignored: undoing something already gone is an impatient double tap, a success rather than a 404.
        await executor.ExecuteAsync(
            """
            delete from suggestion_dismissals
            where user_id = @userId and recipe_id = @recipeId;
            """,
            new { userId, recipeId },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
