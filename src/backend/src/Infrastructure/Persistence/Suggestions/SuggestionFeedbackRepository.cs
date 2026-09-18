using Application.Abstractions;
using Domain.Shared;

namespace Infrastructure.Persistence.Suggestions;

/// <summary>Stores what somebody said "not this" to.</summary>
/// <param name="executor">Runs the SQL inside the request's transaction.</param>
internal sealed class SuggestionFeedbackRepository(DbExecutor executor) : ISuggestionFeedback
{
    public async Task<Result> DismissAsync(
        Guid userId,
        Guid recipeId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        // do nothing on conflict, not "do update": dismissing twice is one
        // dismissal, and restarting the expiry clock would turn a double tap
        // into a longer silence than anybody asked for.
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
        // The row count is ignored on purpose. This is the undo behind a toast,
        // and undoing something that was already gone is what an impatient
        // double tap looks like — a success, not a 404.
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
